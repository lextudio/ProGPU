#pragma once

// Included after the original WebScene fixture helpers. The ordinary corpus
// continues to use its unchanged inputs and entry path.
namespace {
WGPUProcDeviceCreateRenderPipeline demand_create_pipeline{};
std::array<std::uint32_t, 9U> demand_pipeline_counts{};

WGPURenderPipeline observe_3d_pipeline(
    WGPUDevice device, const WGPURenderPipelineDescriptor* descriptor) {
    constexpr std::array<const char*, 9U> labels{{
        "ProGPU native retained 3D line pipeline",
        "ProGPU native retained 3D mesh pipeline",
        "ProGPU native retained 3D front-face mesh pipeline",
        "ProGPU native retained 3D back-face mesh pipeline",
        "ProGPU native retained 3D mesh strip pipeline",
        "ProGPU native retained 3D front-face mesh strip pipeline",
        "ProGPU native retained 3D back-face mesh strip pipeline",
        "ProGPU native retained visible mesh edge pipeline",
        "ProGPU native retained occluded mesh edge pipeline"}};
    auto result = demand_create_pipeline(device, descriptor);
    if (result != nullptr && descriptor->label.data != nullptr) {
        for (std::size_t index = 0U; index < labels.size(); ++index) {
            const auto length = std::strlen(labels[index]);
            if ((descriptor->label.length == length ||
                 descriptor->label.length == WGPU_STRLEN) &&
                std::strncmp(descriptor->label.data, labels[index], length) == 0) {
                ++demand_pipeline_counts[index];
            }
        }
    }
    return result;
}

void* resolve_for_pipeline_demand(void* context, const char* name) {
    void* result = resolve_for_progpu(context, name);
    if (std::strcmp(name, "wgpuDeviceCreateRenderPipeline") == 0) {
        std::memcpy(&demand_create_pipeline, &result, sizeof(result));
        const WGPUProcDeviceCreateRenderPipeline observer = observe_3d_pipeline;
        std::memcpy(&result, &observer, sizeof(result));
    }
    return result;
}

std::vector<std::byte> pipeline_demand_scene(
    bool line_only, std::uint32_t face, bool strip, bool occluded,
    std::uint64_t generation) {
    auto bytes = create_native_3d_scene_stream();
    progpu_native_scene_header header{};
    std::memcpy(&header, bytes.data(), sizeof(header));
    require(header.command_count == 2U, "original 3D command inventory changed");
    header.generation = generation;
    bool mesh_found = false, line_found = false;
    for (std::uint32_t index = 0U; index < 2U; ++index) {
        progpu_native_scene_command command{};
        std::memcpy(&command, bytes.data() + header.command_offset +
            index * header.command_stride, sizeof(command));
        if (command.kind == PROGPU_NATIVE_SCENE_COMMAND_DRAW_LINE_3D_BATCH) {
            line_found = true;
            if (line_only) {
                std::memcpy(bytes.data() + header.command_offset, &command, sizeof(command));
                header.command_count = 1U;
            }
        } else if (command.kind == PROGPU_NATIVE_SCENE_COMMAND_DRAW_MESH_3D_BATCH) {
            mesh_found = true;
            progpu_native_scene_resource resource{};
            std::memcpy(&resource, bytes.data() + header.resource_offset +
                command.resource_index * header.resource_stride, sizeof(resource));
            require(resource.payload_size == 2U * sizeof(progpu_native_scene_mesh_3d),
                "original 3D mesh inventory changed");
            std::array<progpu_native_scene_mesh_3d, 2U> meshes{};
            std::memcpy(meshes.data(), bytes.data() + resource.payload_offset, sizeof(meshes));
            meshes[0].flags = face;
            meshes[0].topology = strip ? PROGPU_NATIVE_MESH_3D_TRIANGLE_STRIP :
                PROGPU_NATIVE_MESH_3D_TRIANGLES;
            if (!occluded) meshes[1].flags &= ~PROGPU_NATIVE_MESH_3D_EDGE_DISPLAY_OCCLUDED;
            std::memcpy(bytes.data() + resource.payload_offset, meshes.data(), sizeof(meshes));
            resource.generation = generation;
            std::memcpy(bytes.data() + header.resource_offset +
                command.resource_index * header.resource_stride, &resource, sizeof(resource));
        }
    }
    require(mesh_found && line_found, "original 3D draw kinds changed");
    std::memcpy(bytes.data(), &header, sizeof(header));
    return bytes;
}

int verify_3d_pipeline_demand(const char* library, const char* output, bool baseline) {
    void* module = dlopen(library, RTLD_NOW | RTLD_LOCAL);
    require(module != nullptr, dlerror());
    provider_api api(module);
    webscene_gpu_provider_options options{};
    options.struct_size = sizeof(options);
    options.required_capabilities = WEBSCENE_GPU_CAPABILITY_WEBGPU;
    auto* provider = api.create(&options);
    require(provider != nullptr, "3D demand provider unavailable");
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
    engine_options.resolve_proc = resolve_for_pipeline_demand;
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
    require(file != nullptr, "3D demand output could not be opened");
    struct test_case { bool line; std::uint32_t face; bool strip; bool occluded; std::uint32_t count; };
    constexpr std::array<test_case, 7U> cases{{
        {true, 0U, false, false, 1U},
        {false, 0U, false, false, 3U},
        {false, PROGPU_NATIVE_MESH_3D_FRONT_FACE, true, false, 4U},
        {false, PROGPU_NATIVE_MESH_3D_BACK_FACE, false, false, 5U},
        {false, 0U, true, true, 6U},
        {false, PROGPU_NATIVE_MESH_3D_FRONT_FACE, false, true, 6U},
        {true, 0U, false, false, 6U}}};
    for (std::uint32_t owner = 0U; owner < 2U; ++owner) {
        demand_pipeline_counts.fill(0U);
        progpu_native_engine* engine{};
        require(progpu_native_dawn_engine_create(&engine_options, &engine) ==
            PROGPU_NATIVE_STATUS_SUCCESS && engine, "3D demand engine creation failed");
        std::uint64_t generation = 0U;
        for (const auto& test : cases) {
            // The unchanged old implementation fails line-only GPU validation.
            // Its separately retained failure is the negative control; compare
            // the five previously admitted mixed scenes against its raw pixels.
            // The ordinary candidate test always executes all seven scenes.
            if (baseline && test.line) continue;
            auto scene = pipeline_demand_scene(test.line, test.face, test.strip,
                test.occluded, ++generation);
            progpu_native_scene_metrics update{};
            update.struct_size = sizeof(update);
            const auto updated = progpu_native_engine_update_scene(
                engine, scene.data(), scene.size(), &update);
            if (updated != PROGPU_NATIVE_STATUS_SUCCESS) {
                std::array<char, 1024U> error{};
                progpu_native_engine_get_last_error(engine, error.data(), error.size());
                std::fprintf(stderr, "3D demand scene %llu: %s\n",
                    static_cast<unsigned long long>(generation), error.data());
            }
            require(updated == PROGPU_NATIVE_STATUS_SUCCESS, "3D demand scene rejected");
            std::vector<std::uint8_t> cold;
            for (std::uint32_t replay = 0U; replay < 2U; ++replay) {
                auto pixels = render_retained_fixture(api, provider, canvas, engine,
                    [&](progpu_native_engine* selected, std::uintptr_t view) {
                        progpu_native_scene_frame frame{};
                        frame.struct_size = sizeof(frame);
                        frame.width = 64U; frame.height = 64U; frame.dpi_scale = 1.0F;
                        frame.target_view = view;
                        frame.clear_color = {0.01F, 0.02F, 0.04F, 1.0F};
                        frame.scene_id = 793U; frame.generation = generation;
                        progpu_native_scene_frame_metrics metrics{};
                        metrics.struct_size = sizeof(metrics);
                        require(progpu_native_engine_render_scene(selected, &frame, &metrics) ==
                            PROGPU_NATIVE_STATUS_SUCCESS && metrics.draw_call_count == (test.line ? 1U : 2U),
                            "3D demand execution failed");
                        require(replay == 0U || metrics.vertex_upload_bytes == 0U,
                            "3D warm replay uploaded a retained page");
                    });
                std::uint32_t total = 0U;
                for (auto count : demand_pipeline_counts) {
                    require(count <= 1U, "3D pipeline was recompiled within its owner");
                    total += count;
                }
                require(total == (baseline ? 9U : test.count), "3D pipeline demand count changed");
                require(baseline || (demand_pipeline_counts[4] == 0U &&
                    demand_pipeline_counts[5] == 0U && demand_pipeline_counts[6] == 0U),
                    "canonical triangle lists compiled strip pipelines");
                if (replay == 0U) cold = pixels;
                else require(cold == pixels, "3D cold/warm complete pixels differ");
                require(std::fwrite(pixels.data(), 1U, pixels.size(), file) == pixels.size(),
                    "3D demand pixel receipt write failed");
            }
        }
        progpu_native_engine_destroy(engine);
    }
    require(std::fclose(file) == 0, "3D demand pixel receipt close failed");
    resolve<WGPUProcQueueRelease>(api, provider, "wgpuQueueRelease")(queue);
    resolve<WGPUProcDeviceRelease>(api, provider, "wgpuDeviceRelease")(device);
    api.destroy(provider);
    dlclose(module);
    std::fprintf(stderr, "3D pipeline demand: 2 independent owners, %u scenes, %u full frames passed\n",
        baseline ? 5U : 7U, baseline ? 20U : 28U);
    return 0;
}
} // namespace
