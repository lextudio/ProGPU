# Windows native package-consumer investigation

## Required acceptance

The native package must execute retained drawing and original owner/generation
GPU queries on Windows, not only export/compile its C++ ABI. This blocks the
LibreWPF native application/package delivery path. No pixel assertion, native
input admission or ten-second readback deadline is removed or relaxed here.

CI run 34566152842 at 280485fb failed differently on its Windows jobs:

- x64 job 103353012827 reported missing independent rectangle ink in the native
  cubic control-hull fixture.
- ARM64 job 103353012738 passed that fixture and native rendering, then crashed
  with 0xC0000005 inside native BeginHitTest.

## Isolated VM reproduction

The unchanged `0.1.0-preview.2992.ci` native package artifact was downloaded from
that run. Its consumer source was staged outside an existing source tree to
avoid inheriting unrelated central package settings. Windows PowerShell 5 refused
script execution; existing PowerShell 7 was used without a policy override.
The Windows 11 ARM64 VM was started normally, with no configuration change.

The isolated consumer builds with zero warnings/errors. Both ARM64 and emulated
x64 execute `--mil-drawing-group-only`, including cubic coverage, native retained
rendering and owner-query snapshot/generation checks. ARM64 passes rendering but
times out polling its first GPU owner query. x64 exits 0 with
`native GPU owner snapshot/generation isolation` and the final package success
marker. This is a Parallels D3D12 discrete adapter, not CI's Microsoft software
adapter; the runs overlapped and are not controlled performance benchmarks.
These results do not qualify final-head packages or resolve the CI failures.

## DXC selection is unavailable in the shipped native dependency

Windows SDK 10.0.26100.0 has matching ARM64/x64 DXC and DXIL DLLs (1.8.2502.11).
An isolated managed diagnostic requested DXC via the pinned native extension ABI,
using explicit absolute paths to those DLLs. Both comparison processes still
loaded FXC and were intentionally stopped after verifying their exact diagnostic
module paths and absence of DXC. They are not successful DXC runs.

A small native-log initialization probe exits 0 but emits this authoritative
reason: DXC was requested, yet `dxc_shader_compiler` is disabled in wgpu-hal,
followed by `Using FXC for shader compilation`. Changing managed configuration
alone therefore cannot select DXC in the shipped Silk.NET native package. The
diagnostic patch was reverted; production compiler defaults are unchanged.

The exact ABI pin remains wgpu-native 33133da4ec5a0174cb21539ef2d3346f75200411,
with wgpu revision 87576b72b37c6b78b41104eb25fc31893af94092. Its dependency
manifest exposes the optional `dxc_shader_compiler` feature. A DXC comparison
requires an ABI-identical native build with that feature enabled; blindly using
a newer incompatible wgpu DLL is not admissible. The VM currently has no Rust
toolchain. This discovery explains compiler selection, not yet the pixel failure,
access violation or readback timeout.

## Diagnostics added without changing acceptance

The consumer now prints existing typed WebGPU error/device-loss notifications,
actual RGBA and adapter identity on cubic failure, and cold query submission time
separately from the existing readback deadline. Timeout details include device
loss state and adapter/backend. The updated source-reference consumer compiles
with zero warnings/errors. New runtime and CI results remain required.

Reproduction artifacts are under LibreWPF
`artifacts/native-windows-consumer.akdIwM`, including scripts, original package
inputs, logs, module inventories and the reverted diagnostic patch. The original
failed staging directory and all comparison outputs remain preserved.
