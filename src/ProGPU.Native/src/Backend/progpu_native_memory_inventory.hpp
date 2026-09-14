#pragma once

#include <algorithm>
#include <atomic>
#include <cstdint>
#include <functional>
#include <limits>
#include <stdexcept>
#include <vector>

namespace progpu::native {

inline std::atomic<std::uint64_t> next_memory_inventory_identity{1U};

// Logical texel storage only. O(L) dependent mip-prefix work, constant space.
// Three-dimensional depth shrinks; array layers do not. Unknown/opaque formats
// are rejected by the caller, never assigned an invented byte width.
inline bool logical_texture_bytes(std::uint32_t width, std::uint32_t height,
    std::uint32_t depth, std::uint32_t levels, std::uint32_t samples,
    std::uint32_t texel_bytes, bool volume, std::uint64_t& result) noexcept {
    if (width == 0U || height == 0U || depth == 0U || levels == 0U ||
        levels > 32U || samples == 0U || texel_bytes == 0U) return false;
    std::uint64_t total = 0U;
    constexpr auto maximum = std::numeric_limits<std::uint64_t>::max();
    for (std::uint32_t level = 0U; level < levels; ++level) {
        std::uint64_t bytes = width;
        for (const auto factor : {height, depth, samples, texel_bytes}) {
            if (bytes > maximum / factor) return false;
            bytes *= factor;
        }
        if (total > maximum - bytes) return false;
        total += bytes;
        width = std::max(1U, width / 2U);
        height = std::max(1U, height / 2U);
        if (volume) depth = std::max(1U, depth / 2U);
    }
    result = total;
    return true;
}

class gpu_memory_inventory final {
public:
    const std::uint64_t identity = next_memory_inventory_identity.fetch_add(1U, std::memory_order_relaxed);

    void reset() noexcept { buffers_.clear(); textures_.clear(); borrowed_views_.clear(); }
    void buffer(WGPUBuffer value) { if (value != nullptr) buffers_.push_back(value); }
    void texture(WGPUTexture value) { if (value != nullptr) textures_.push_back(value); }
    void borrowed_view(WGPUTextureView value) { if (value != nullptr) borrowed_views_.push_back(value); }

    // Identity sorting is data-dependent; WebGPU descriptor queries are opaque
    // API calls, not independent compute lanes. Scratch retains its high-water
    // capacity and aliases never allocate additional resource accounting nodes.
    progpu_native_gpu_memory_snapshot summarize() {
        deduplicate(buffers_);
        deduplicate(textures_);
        deduplicate(borrowed_views_);
        progpu_native_gpu_memory_snapshot result{};
        result.struct_size = sizeof(result);
        result.engine_id = identity;
        result.owned_buffer_count = buffers_.size();
        result.owned_texture_count = textures_.size();
        result.borrowed_view_count = borrowed_views_.size();
        result.inventory_storage_bytes = buffers_.capacity() * sizeof(WGPUBuffer) +
            textures_.capacity() * sizeof(WGPUTexture) + borrowed_views_.capacity() * sizeof(WGPUTextureView);
        for (auto value : buffers_) add_bytes(result.owned_buffer_bytes, wgpuBufferGetSize(value));
        for (auto value : textures_) {
            std::uint32_t texel_bytes = 0U;
            switch (wgpuTextureGetFormat(value)) {
            case WGPUTextureFormat_R8Unorm: texel_bytes = 1U; break;
            case WGPUTextureFormat_RGBA8Unorm:
            case WGPUTextureFormat_RGBA8UnormSrgb:
            case WGPUTextureFormat_BGRA8Unorm:
            case WGPUTextureFormat_BGRA8UnormSrgb: texel_bytes = 4U; break;
            case WGPUTextureFormat_RGBA32Uint: texel_bytes = 16U; break;
            default: break; // Includes implementation-dependent Depth24Plus.
            }
            std::uint64_t bytes = 0U;
            if (!logical_texture_bytes(wgpuTextureGetWidth(value), wgpuTextureGetHeight(value),
                    wgpuTextureGetDepthOrArrayLayers(value), wgpuTextureGetMipLevelCount(value),
                    wgpuTextureGetSampleCount(value), texel_bytes,
                    wgpuTextureGetDimension(value) == WGPUTextureDimension_3D, bytes)) {
                ++result.unquantified_texture_count;
            } else {
                add_bytes(result.owned_texture_bytes, bytes);
            }
        }
        return result;
    }

private:
    static void add_bytes(std::uint64_t& total, std::uint64_t bytes) {
        if (bytes > std::numeric_limits<std::uint64_t>::max() - total)
            throw std::overflow_error("Native GPU memory total overflow.");
        total += bytes;
    }
    template<class Handle> static void deduplicate(std::vector<Handle>& values) {
        std::sort(values.begin(), values.end(), std::less<Handle>{});
        values.erase(std::unique(values.begin(), values.end()), values.end());
    }
    std::vector<WGPUBuffer> buffers_;
    std::vector<WGPUTexture> textures_;
    std::vector<WGPUTextureView> borrowed_views_;
};

} // namespace progpu::native
