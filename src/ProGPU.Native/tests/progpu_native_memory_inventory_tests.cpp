#include "progpu_native.h"
#include "progpu_webgpu_compat.hpp"
#include "progpu_native_memory_inventory.hpp"

// Production inline bodies above already capture provider dispatch. Access the
// actual procedure-table fields below when installing fixture descriptors.
#undef wgpuBufferGetSize
#undef wgpuTextureGetFormat
#undef wgpuTextureGetDimension
#undef wgpuTextureGetWidth
#undef wgpuTextureGetHeight
#undef wgpuTextureGetDepthOrArrayLayers
#undef wgpuTextureGetMipLevelCount
#undef wgpuTextureGetSampleCount

#include <cstdlib>

namespace {
void require(bool condition) { if (!condition) std::abort(); }
struct buffer_record { std::uint64_t size; };
struct texture_record {
    std::uint32_t width, height, depth, levels, samples;
    WGPUTextureFormat format;
    WGPUTextureDimension dimension;
};
std::uint32_t buffer_reads = 0U, texture_reads = 0U;
std::uint64_t buffer_size(WGPUBuffer value) { ++buffer_reads; return reinterpret_cast<buffer_record*>(value)->size; }
texture_record& texture(WGPUTexture value) { return *reinterpret_cast<texture_record*>(value); }
WGPUTextureFormat texture_format(WGPUTexture value) { ++texture_reads; return texture(value).format; }
WGPUTextureDimension texture_dimension(WGPUTexture value) { return texture(value).dimension; }
std::uint32_t texture_width(WGPUTexture value) { return texture(value).width; }
std::uint32_t texture_height(WGPUTexture value) { return texture(value).height; }
std::uint32_t texture_depth(WGPUTexture value) { return texture(value).depth; }
std::uint32_t texture_levels(WGPUTexture value) { return texture(value).levels; }
std::uint32_t texture_samples(WGPUTexture value) { return texture(value).samples; }
}

void test_native_memory_inventory() {
    using progpu::native::logical_texture_bytes;
    std::uint64_t bytes = 123U;
    require(logical_texture_bytes(8, 4, 3, 4, 1, 4, false, bytes));
    require(bytes == (32U + 8U + 2U + 1U) * 3U * 4U);
    require(logical_texture_bytes(8, 4, 4, 4, 1, 4, true, bytes));
    require(bytes == (128U + 16U + 2U + 1U) * 4U);
    require(logical_texture_bytes(8, 4, 1, 1, 4, 4, false, bytes) && bytes == 512U);
    bytes = 123U;
    require(!logical_texture_bytes(0, 4, 1, 1, 1, 4, false, bytes) && bytes == 123U);
    require(!logical_texture_bytes(8, 4, 1, 33, 1, 4, false, bytes) && bytes == 123U);
    require(!logical_texture_bytes(UINT32_MAX, UINT32_MAX, UINT32_MAX, 1, 1, 16, false, bytes) && bytes == 123U);

    progpu::native::webgpu::dispatch dispatch{};
    dispatch.wgpuBufferGetSize = buffer_size;
    dispatch.wgpuTextureGetFormat = texture_format;
    dispatch.wgpuTextureGetDimension = texture_dimension;
    dispatch.wgpuTextureGetWidth = texture_width;
    dispatch.wgpuTextureGetHeight = texture_height;
    dispatch.wgpuTextureGetDepthOrArrayLayers = texture_depth;
    dispatch.wgpuTextureGetMipLevelCount = texture_levels;
    dispatch.wgpuTextureGetSampleCount = texture_samples;
    const progpu::native::webgpu::dispatch_scope scope(&dispatch);
    buffer_record first{64U}, second{1024U};
    texture_record rgba{8U, 4U, 3U, 4U, 1U, WGPUTextureFormat_RGBA8Unorm, WGPUTextureDimension_2D};
    texture_record opaque{8U, 4U, 1U, 1U, 1U, WGPUTextureFormat_Depth24Plus, WGPUTextureDimension_2D};
    progpu::native::gpu_memory_inventory inventory;
    std::uint64_t capacity = 0U;
    for (int iteration = 0; iteration < 16; ++iteration) {
        inventory.reset();
        inventory.buffer(nullptr);
        inventory.buffer(reinterpret_cast<WGPUBuffer>(&first));
        inventory.buffer(reinterpret_cast<WGPUBuffer>(&second));
        inventory.buffer(reinterpret_cast<WGPUBuffer>(&first));
        inventory.texture(reinterpret_cast<WGPUTexture>(&rgba));
        inventory.texture(reinterpret_cast<WGPUTexture>(&rgba));
        inventory.texture(reinterpret_cast<WGPUTexture>(&opaque));
        inventory.borrowed_view(reinterpret_cast<WGPUTextureView>(&rgba));
        inventory.borrowed_view(reinterpret_cast<WGPUTextureView>(&rgba));
        buffer_reads = texture_reads = 0U;
        const auto snapshot = inventory.summarize();
        require(snapshot.struct_size == sizeof(snapshot) && snapshot.engine_id != 0U);
        require(snapshot.owned_buffer_count == 2U && snapshot.owned_buffer_bytes == 1088U && buffer_reads == 2U);
        require(snapshot.owned_texture_count == 2U && snapshot.owned_texture_bytes == 516U && texture_reads == 2U);
        require(snapshot.unquantified_texture_count == 1U && snapshot.borrowed_view_count == 1U);
        if (iteration == 0) capacity = snapshot.inventory_storage_bytes;
        require(snapshot.inventory_storage_bytes == capacity);
    }
    inventory.reset();
    const auto empty = inventory.summarize();
    require(empty.owned_buffer_count == 0U && empty.owned_texture_count == 0U && empty.borrowed_view_count == 0U);
    require(empty.inventory_storage_bytes == capacity);
    progpu::native::gpu_memory_inventory other;
    require(other.identity != inventory.identity);
    buffer_record oversized{UINT64_MAX};
    inventory.buffer(reinterpret_cast<WGPUBuffer>(&oversized));
    inventory.buffer(reinterpret_cast<WGPUBuffer>(&first));
    bool overflow = false;
    try { (void)inventory.summarize(); }
    catch (const std::overflow_error&) { overflow = true; }
    require(overflow);
}
