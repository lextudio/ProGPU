# Native process render synchronization lifetime

## Core application dependency

The ProGPU.Wpf.ToolkitApp native MIL diagnostic reached AvalonDock's floating
editor, then intermittently terminated during process cleanup with
`std::recursive_mutex lock failed: Invalid argument`. The process-wide render
guard is shared across independently owned WGPU contexts in the non-Dawn native
backend. A caller can register its cleanup callback before the first renderer
call. In that ordering, an ordinary function-static mutex destructor runs before
the caller's cleanup callback, which can still enter a native render scope.

An isolated process reproduced the exact error and exit code 134 with the old
mutex. The same late-cleanup sequence exits 0 after this change. This identifies
the guard lifetime error; it does not prove that every Toolkit teardown path is
qualified.

## Implementation

`process_render_mutex()` constructs its recursive mutex once in aligned static
byte storage. The storage has no destructor, so the mutex remains usable during
late process cleanup. Function-static pointer initialization still serializes
construction. Only this synchronization primitive has process lifetime; engine,
device, buffer, window and callback owners retain their normal disposal paths.
The Dawn provider uses its independent synchronization path.

The managed backend already holds its render monitor in a static managed object;
there is no C++ function-static destructor in that path. No managed renderer
change is required for this ordering.

`progpu_native_process_synchronization_tests` registers a late cleanup callback
before first guard use, exercises nested scopes and four concurrent workers,
then re-enters the guard at process exit. It runs on native desktop CTest lanes;
the browser Emscripten target does not use this process-exit test.

## Qualification — 2026-09-14

The isolated before/after process repro returned 134/0. The complete macOS ARM64
native CTest set passes 20/20, including the new process test, after the C++
fix. Final Toolkit teardown, Windows/Linux builds, CI and package qualification
remain separate gates. The latest published ProGPU CI head predates this fix and
has an MSVC signedness failure in an unrelated MIL test assertion; the assertion
is corrected in the same next commit without changing its expected value.
