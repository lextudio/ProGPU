#include "progpu_native_webgpu_synchronization.hpp"

#include <cstdlib>
#include <thread>
#include <vector>

namespace {

void late_client_cleanup() {
    // Register before first use: ordinary function-static destruction would
    // destroy the shared mutex before this callback. Recursion remains required.
    progpu::native::webgpu::process_render_scope outer;
    progpu::native::webgpu::process_render_scope nested;
}

} // namespace

int main() {
    if (std::atexit(late_client_cleanup) != 0) return 1;
    int protected_count = 0;
    std::vector<std::thread> workers;
    for (int thread = 0; thread < 4; ++thread) {
        workers.emplace_back([&] {
            for (int i = 0; i < 1000; ++i) {
                progpu::native::webgpu::process_render_scope outer;
                late_client_cleanup();
                ++protected_count;
            }
        });
    }
    for (auto& thread : workers) thread.join();
    return protected_count == 4000 ? 0 : 2;
}
