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
a newer incompatible wgpu DLL is not admissible. At this checkpoint the VM had no Rust
toolchain. This discovery explains compiler selection, not yet the pixel failure,
access violation or readback timeout.

## Feature-enabled comparison and point-query separation

An isolated ARM64 Rust toolchain was subsequently installed under
`C:\ProGPU.WgpuToolchain-akdIwM`, without persistent PATH or global tool changes.
The exact pinned wgpu-native/wgpu/header sources were built with their optional
DXC dependency feature, using the existing Visual Studio environment and an
isolated build-time libclang. No foreign implementation source was modified.
The generated optional-dependency lock file was preserved and the resumed build
used `--locked`. Its DLL SHA256 is
`52b3eea2261daabc3980e9690cc66b1bcce5eada451808882504e300f12db84d`.

The independent probe now reports `Using DXC for shader compilation` and loads
the explicit SDK ARM64 compiler. The unchanged native consumer then fails in
render/compute pipeline creation with 0x80004005 and aborts after invalid pipeline
use. DXC is therefore not qualified on this Parallels adapter. Neither that DLL
nor the diagnostic managed compiler selection is shipped or enabled by default.

A separate original-backend x64 run explicitly selects Microsoft Basic Render
Driver in the VM. It passes the cubic rectangle check, unlike CI run 34758568841,
where both Windows architectures now fail that same assertion. The VM's newer
Windows/software driver is not an exact runner match. Its owner-query creation
and the ordinary ARM64 adapter's query creation both remain slow before submission;
overlapping diagnostic processes are not controlled performance measurements.

The bounded product change separates point queries from region classification:
`cs_point` calls one shared traversal with constant point mode, while `cs_main`
retains region selection. Managed point/list queries share their cached point
pipeline. Both native providers keep the point pipeline and lazily create the
general region pipeline with the existing layout/index; disposal releases both.
No geometry approximation, CPU query fallback, compiler default or deadline change
is introduced. Traversal complexity and stable query buffers are unchanged; at
most two query pipelines are retained per native engine instead of one.

Validation: managed Vector and source-reference consumer builds have zero
warnings/errors; both native provider libraries compile. All 122 managed GPU
hit-test tests pass, including a new point/list/region/point pipeline-reuse test.
The rebuilt native Metal consumer exits 0 through the cubic fixture, retained
rendering and original owner/generation/participation query checks. These staged
results do not qualify Windows or exact final packages. Windows CI and the
independent missing-rectangle failure remain required before merge.

The committed point-query change was then rebuilt with Windows ARM64 MSVC in
`C:\ProGPU.PointQuery-532a95ea`, including both provider DLLs. The first configure
omitted the optional Dawn header input; the existing build was reconfigured with
the exact pinned header and both targets completed. The comparison retains the
original packaged wgpu-native DLL. It passes cubic/retained rendering and submits
the first point query after 44,723.421 ms. Completion of all owner/region checks
is still pending; this is not an acceptable cold-start performance claim.
The superseded original ARM64/software-x64 diagnostics were explicitly stopped
after verifying their exact managed module paths, with logs preserved. They had
not submitted the first query after 14m32s/12m36s total elapsed time and are not
passes or controlled benchmarks.

CI run 34761357934 at 532a95ea additionally reports one Linux test failure:
`WinUiCompositionTests.LinearAndRadialGradientsRenderThroughRetainedWebGpuScene`
passes its pixel assertions but fails the final stable scene-cache-hit assertion.
The assertion now includes the existing cache-miss reason without changing its
requirement or adding a retry. All 39 WinUI composition tests pass locally.
The Linux failure is not diagnosed or waived by that local result.

## Diagnostics added without changing acceptance

The subsequent cbb diagnostic head fails on both Windows CI architectures with
RGBA `(0,0,0,255)`, `deviceLost=False`, D3D12 and Microsoft Basic Render Driver.
Failure-only pixel inventory now skips all-black blocks with `Vector<uint>` and
reports exact nonblack bounds/count together with the existing native draw/upload
counters. The original white-pixel and cubic-outside assertions are unchanged.
This distinguishes an empty target from misplaced ink; it is not a raster fix.
The consumer compiles with zero warnings/errors. The newer query-family work and
terminal point-only crash evidence are recorded in
[query pipeline specialization](native-mil-query-pipeline-specialization.md).

The consumer now prints existing typed WebGPU error/device-loss notifications,
actual RGBA and adapter identity on cubic failure, and cold query submission time
separately from the existing readback deadline. Timeout details include device
loss state and adapter/backend. The updated source-reference consumer compiles
with zero warnings/errors. New runtime and CI results remain required.

Reproduction artifacts are under LibreWPF
`artifacts/native-windows-consumer.akdIwM`, including scripts, original package
inputs, logs, module inventories and the reverted diagnostic patch. The original
failed staging directory and all comparison outputs remain preserved.
