#include "progpu_webgpu_compat.hpp"

#include <cstring>

namespace {
WGPUComputePassEncoder received_pass = nullptr;
WGPUBuffer received_buffer = nullptr;
std::uint64_t received_offset = 0U;
std::uint32_t call_count = 0U;

void dispatch_indirect(WGPUComputePassEncoder pass, WGPUBuffer buffer, std::uint64_t offset) {
    received_pass = pass;
    received_buffer = buffer;
    received_offset = offset;
    ++call_count;
}

void unused_proc() {}

void* resolve(void* context, const char* name) {
    if (std::strcmp(name, "wgpuComputePassEncoderDispatchWorkgroupsIndirect") == 0) {
        return context == nullptr ? reinterpret_cast<void*>(&dispatch_indirect) : nullptr;
    }
    return reinterpret_cast<void*>(&unused_proc);
}

void require(bool value) { if (!value) std::abort(); }
}

// Called by the native provider contract executable. No device or GPU emulation:
// only exact procedure selection, arguments and scoped provider ownership.
void test_dawn_indirect_dispatch() {
    progpu::native::webgpu::dispatch provider{};
    require(!provider.load(&provider, resolve));
    require(provider.load(nullptr, resolve));
    require(progpu::native::webgpu::current_dispatch == nullptr);
    {
        progpu::native::webgpu::dispatch_scope scope(&provider);
        auto pass = reinterpret_cast<WGPUComputePassEncoder>(std::uintptr_t{23U});
        auto buffer = reinterpret_cast<WGPUBuffer>(std::uintptr_t{17U});
        constexpr std::uint64_t offset = 0x100000004ULL;
        wgpuComputePassEncoderDispatchWorkgroupsIndirect(pass, buffer, offset);
        require(call_count == 1U && received_pass == pass &&
            received_buffer == buffer && received_offset == offset);
    }
    require(progpu::native::webgpu::current_dispatch == nullptr);
}
