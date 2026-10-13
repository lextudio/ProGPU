#pragma once

// Additive pipeline ownership checks; the original corpus remains unchanged.
namespace {
WGPUProcDeviceCreateRenderPipeline mask_demand_create_pipeline{};
std::array<std::uint32_t, 3U> mask_demand_pipeline_counts{};

WGPURenderPipeline observe_mask_pipeline(
    WGPUDevice device, const WGPURenderPipelineDescriptor* descriptor) {
    constexpr std::array<const char*, 3U> labels{{
        "ProGPU native retained image pipeline",
        "ProGPU native premultiplied group composite pipeline",
        "ProGPU native masked premultiplied group composite pipeline"}};
    auto result = mask_demand_create_pipeline(device, descriptor);
    if (result != nullptr && descriptor->label.data != nullptr) {
        for (std::size_t index = 0U; index < labels.size(); ++index) {
            const auto length = std::strlen(labels[index]);
            if ((descriptor->label.length == length ||
                 descriptor->label.length == WGPU_STRLEN) &&
                std::strncmp(descriptor->label.data, labels[index], length) == 0) {
                ++mask_demand_pipeline_counts[index];
            }
        }
    }
    return result;
}

void* resolve_for_mask_pipeline_demand(void* context, const char* name) {
    void* result = resolve_for_progpu(context, name);
    if (std::strcmp(name, "wgpuDeviceCreateRenderPipeline") == 0) {
        std::memcpy(&mask_demand_create_pipeline, &result, sizeof(result));
        const WGPUProcDeviceCreateRenderPipeline observer = observe_mask_pipeline;
        std::memcpy(&result, &observer, sizeof(result));
    }
    return result;
}

int verify_mask_pipeline_demand(const char* library, const char* output, bool baseline) {
    void* module = dlopen(library, RTLD_NOW | RTLD_LOCAL);
    require(module != nullptr, dlerror());
    provider_api api(module);
    webscene_gpu_provider_options options{};
    options.struct_size = sizeof(options);
    options.required_capabilities = WEBSCENE_GPU_CAPABILITY_WEBGPU;
    auto* provider = api.create(&options);
    require(provider != nullptr, "Mask demand provider unavailable");
    auto device = create_device(api, provider);
    auto queue = resolve<WGPUProcDeviceGetQueue>(api, provider, "wgpuDeviceGetQueue")(device);
    resolver_context resolver{&api, provider};
    progpu_native_dawn_engine_options engine_options{};
    engine_options.struct_size = sizeof(engine_options);
    engine_options.native_abi_version = PROGPU_NATIVE_ABI_VERSION;
    engine_options.adapter_abi_version = PROGPU_NATIVE_DAWN_ADAPTER_ABI_VERSION;
    engine_options.provider_abi_version = WEBSCENE_GPU_PROVIDER_ABI_VERSION;
    engine_options.target_format = PROGPU_NATIVE_TEXTURE_FORMAT_BGRA8_UNORM;
    engine_options.resolver_context = &resolver;
    engine_options.resolve_proc = resolve_for_mask_pipeline_demand;
    engine_options.instance = reinterpret_cast<std::uintptr_t>(api.get_instance(provider));
    engine_options.device = reinterpret_cast<std::uintptr_t>(device);
    engine_options.queue = reinterpret_cast<std::uintptr_t>(queue);
    webscene_gpu_canvas_configuration canvas{};
    canvas.struct_size = sizeof(canvas);
    canvas.device = engine_options.device;
    canvas.usage = WGPUTextureUsage_RenderAttachment | WGPUTextureUsage_CopySrc;
    canvas.pixel_format = WEBSCENE_GPU_PIXEL_FORMAT_BGRA8_UNORM;
    canvas.alpha_mode = WEBSCENE_GPU_ALPHA_MODE_PREMULTIPLIED;
    canvas.buffer_count = 2U;
    auto* file = std::fopen(output, "wb");
    require(file != nullptr, "Mask demand output could not be opened");
    // All geometry, masks, images and styles come from the original independent
    // fixtures. Two owner orders exercise bindings-first and layers-first.
    constexpr std::array<std::array<unsigned, 5>, 2> orders{{
        {{0U, 1U, 2U, 0U, 1U}}, {{2U, 0U, 1U, 2U, 0U}}}};
    for (std::uint32_t owner = 0U; owner < orders.size(); ++owner) {
        mask_demand_pipeline_counts.fill(0U);
        progpu_native_engine* engine{};
        require(progpu_native_dawn_engine_create(&engine_options, &engine) ==
            PROGPU_NATIVE_STATUS_SUCCESS && engine, "Mask demand engine creation failed");
        bool saw_image = false, saw_layer = false;
        std::uint64_t generation = 0U;
        for (const auto kind : orders[owner]) {
            auto scene = kind == 0U ? create_semantic_state_mask_scene_stream(64U, 64U) :
                kind == 1U ? create_semantic_state_mask_media_scene_stream(64U, 64U) :
                create_semantic_masked_layer_scene_stream();
            progpu_native_scene_header header{};
            std::memcpy(&header, scene.data(), sizeof(header));
            header.generation = ++generation;
            std::memcpy(scene.data(), &header, sizeof(header));
            progpu_native_scene_metrics update{};
            update.struct_size = sizeof(update);
            require(progpu_native_engine_update_scene(engine, scene.data(), scene.size(), &update) ==
                PROGPU_NATIVE_STATUS_SUCCESS, "Mask demand scene rejected");
            saw_image |= kind == 1U;
            saw_layer |= kind == 2U;
            std::vector<std::uint8_t> cold;
            for (std::uint32_t replay = 0U; replay < 2U; ++replay) {
                auto pixels = render_retained_fixture(api, provider, canvas, engine,
                    [&](progpu_native_engine* selected, std::uintptr_t view) {
                        progpu_native_scene_frame frame{};
                        frame.struct_size = sizeof(frame);
                        frame.width = 64U; frame.height = 64U; frame.dpi_scale = 1.0F;
                        frame.target_view = view;
                        frame.clear_color = {0.01F, 0.02F, 0.04F, 1.0F};
                        frame.scene_id = header.scene_id; frame.generation = generation;
                        progpu_native_scene_frame_metrics metrics{};
                        metrics.struct_size = sizeof(metrics);
                        const auto status = progpu_native_engine_render_scene(selected, &frame, &metrics);
                        if (status != PROGPU_NATIVE_STATUS_SUCCESS) {
                            std::array<char, 1024U> error{};
                            progpu_native_engine_get_last_error(engine, error.data(), error.size());
                            std::fprintf(stderr, "Mask demand kind %u: %s\n", kind, error.data());
                        }
                        require(status == PROGPU_NATIVE_STATUS_SUCCESS && metrics.draw_call_count > 0U,
                            "Mask demand execution failed");
                        require(replay == 0U || metrics.vertex_upload_bytes == 0U,
                            "Mask warm replay uploaded a retained page");
                    });
                const std::array<std::uint32_t, 3U> expected = baseline
                    ? std::array<std::uint32_t, 3U>{1U, 1U, 1U}
                    : std::array<std::uint32_t, 3U>{saw_image ? 1U : 0U,
                        saw_layer ? 1U : 0U, saw_layer ? 1U : 0U};
                if (mask_demand_pipeline_counts != expected) {
                    std::fprintf(stderr, "Mask demand owner=%u kind=%u actual=%u/%u/%u expected=%u/%u/%u\n",
                        owner, kind, mask_demand_pipeline_counts[0], mask_demand_pipeline_counts[1],
                        mask_demand_pipeline_counts[2], expected[0], expected[1], expected[2]);
                }
                require(mask_demand_pipeline_counts == expected,
                    "Mask bindings compiled an unused pipeline or failed a later draw demand");
                if (replay == 0U) cold = pixels;
                else require(cold == pixels, "Mask cold/warm complete pixels differ");
                require(std::fwrite(pixels.data(), 1U, pixels.size(), file) == pixels.size(),
                    "Mask demand pixel receipt write failed");
            }
        }
        progpu_native_engine_destroy(engine);
    }
    require(std::fclose(file) == 0, "Mask demand pixel receipt close failed");
    resolve<WGPUProcQueueRelease>(api, provider, "wgpuQueueRelease")(queue);
    resolve<WGPUProcDeviceRelease>(api, provider, "wgpuDeviceRelease")(device);
    api.destroy(provider);
    dlclose(module);
    std::fprintf(stderr, "Mask pipeline demand: 2 independent owners, 10 scenes, 20 full frames passed\n");
    return 0;
}
} // namespace
