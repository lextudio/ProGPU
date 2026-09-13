# Native path first-frame isolation

## Blocking acceptance path

The core application is `ProGPU.Wpf.ShowcaseApp`; the user action is its first
native MIL path drawing. The source path is retained MIL geometry through the
native path coverage buffer, R8 atlas and analytic draw. At `e56d45c5`, both
Windows package jobs fail the original cubic/independent-rectangle assertion
with an entirely black target, before any hit query. Forty other PR checks pass.
The package jobs remain required and no first frame may be discarded or retried
as a successful substitute.

## Bounded diagnostic

### Native resource-reference comparison, 2026-09-13

Run [34774316909](https://github.com/wieslawsoltes/ProGPU/actions/runs/34774316909)
passes explicit native-equivalent pipeline/bind-group layouts and the exact
`wgpuQueueSubmitForIndex` / `wgpuDevicePoll` completion-token sequence on both
Windows architectures against package 3005. Native drawing still fails on x64
(direct-path device loss and an entirely black original cubic fixture); ARM64
passes in this run but failed the preceding identical-package comparison.
This does not qualify either current-head package or reproducible cold startup.

`--path-native-release-probe` adds one controlled ownership difference to the
passing exact-submission probe: release the caller command-buffer reference and
all five temporary raster buffer references plus their bind group immediately
after submission, before waiting. The tiny diagnostic owns raw WebGPU references
and releases them exactly once, without `BufferDestroy` or managed deferred
disposal. Only the retained atlas and final frame are read in this case; released
buffers are never accessed. Ordinary probes still verify raw coverage separately.
No renderer, shader, dependency, deadline, fallback or acceptance assertion changes.
The manual workflow's `lifetime` selection runs that comparison and both original
native probes in independent processes, keeping the ten-minute bound.

Release compilation reports zero warnings/errors. Metal exact-submission,
early-release and raw-coverage probes all pass. Hosted lifetime results remain
required. This is diagnostic coverage of the common WebGPU ownership contract,
not a one-sided production rendering change or a demonstrated lifetime defect.

Hosted [run 34775162633](https://github.com/wieslawsoltes/ProGPU/actions/runs/34775162633)
now passes the retained-reference baseline on both architectures. Releasing the
command plus all raster references before completion produces an access violation
on x64; the native x64 fixtures still fail. ARM64 passes that comparison but the
original cubic remains black. This reproduces a submission-lifetime-sensitive
failure outside C++, not proof of which reference is causal. The subsequent
`--path-native-release-buffers-probe` and `--path-native-release-command-probe`
split those two changes. Both build without warnings/errors and pass on Metal;
Windows results determine the required retention scope. No production lifetime
fix has been qualified yet.

### Submission-bound native raster retention

The x64 split comparison in
[run 34775504926](https://github.com/wieslawsoltes/ProGPU/actions/runs/34775504926)
passes the retained baseline and command-buffer-only release. Releasing only the
five raster buffers plus bind group produces an entirely black frame. Original
native rectangle/cubic probes likewise remain black. This isolates the observed
failure to early raster-resource release, independently of C++ preparation and
command-buffer ownership; it does not identify an individual buffer or a driver
internal defect.

Native path, clip and glyph staging now acquire one recording lease per uncached
raster batch. Active leases survive intermediate submissions and completion
polls. Closing a lease assigns the latest consumed submission, or the next one
for a borrowed semantic encoder. Failed independent setup without a submission
releases its batch immediately. Existing eight-submission polling and the
64-submission drain retire completed batches; explicit latest-token waits retire
them too. Engine disposal drops an unsubmitted encoder and drains submitted work
before releasing retained batches. There is no per-draw wait, warm-up, extra GPU
submission, shader change or CPU fallback. Cached path/glyph replay allocates no
new batch. Publication is amortized O(1); retirement is O(B) over B live batches,
whose residency follows current unsubmitted scene work and the existing bounded
submission window.

Both native wgpu-native and Dawn builds use this lease. Browser WebGPU retains
its existing encoded-reference ownership because it has no synchronous native
completion polling; it must not acquire an undrainable native retirement queue.
Managed `GpuBuffer.Dispose` already uses the backend's deferred-release path,
which is why the diagnostic needs an explicitly owned raw buffer to reproduce
early release. No corresponding managed rendering algorithm change is required.

Both native providers compile on Metal. All 20 CTest cases pass, including active
recording, borrowed-future completion, out-of-order publication, cancellation and
repeated periodic retirement. The full Metal native package consumer passes its
original frame and native owner/generation/participation/region checks. Windows
native compilation and original fixtures are running; final-head CI/package and
application qualification remain mandatory before dependency pins or merges.

MSVC ARM64 now compiles both native providers. The original direct rectangle and
cubic assertions pass with the normal Parallels Display Adapter. The staged
wgpu-native DLL SHA256 is
`079FB052B7CA73F95AAB2BFF1F1F27D9EFA4A464599F512B5C1ACA315059CF1E`.
The actual LibreWPF source-host/device-recovery run also passes on Metal with
`f5dcfb15`. These staged builds are not final package qualification. The software
adapter comparison remains running. A follow-up avoids scanning the retirement
list at every recording-lease close when that batch's completion has not yet
been observed; periodic retirement and already-completed lease cleanup are kept.

`ProGPU.Native.PackageConsumer --path-coverage-probe` executes the packaged
`ProGPU.Backend.Shaders.PathRasterizerShader`, entry `cs_main_ordinary`, with
its original five-binding storage ABI, 16x16 workgroup and eight-by-eight sample
grid. A closed four-segment rectangle fills a 64x16 coverage region, packed at
256 bytes per row. The same command buffer copies that region to `(17,19)` in a
new 1024x1024 R8 atlas. Readback then compares known interior/exterior samples
from both the raw coverage and copied texture, plus an untouched atlas sample.
Expected values are `(255,0)`, `(255,0)` and `0` respectively.

This is an original test over existing ProGPU source, not a shader reduction,
CPU rasterizer, new runtime API or production fallback. It separates basic
canonical rasterization from partial atlas transfer. It does not exercise the
native semantic material bindings, cubic math, fragment sampling or original
failed frame, and cannot qualify those contracts.

CI invokes it in a separate process only after a Windows package job has
already failed. The original assertion, exit status, job deadline, compiler,
dependencies and later qualification gates are unchanged. A successful
diagnostic does not make the failed job successful. No production warm-up,
extra submission or readback was introduced.

## Local results, 2026-09-13

- Release build: zero warnings/errors.
- Metal, Apple M3 Pro: exact expected samples, exit 0.
- Exact package `0.1.0-preview.3000.ci`, Windows ARM64, Parallels Display Adapter:
  exact expected samples, exit 0.
- Same packaged original wgpu runtime, forced Microsoft Basic Render Driver:
  exact expected samples, exit 0. Only the isolated diagnostic Backend assembly
  selects the software adapter; it is not shipped.

Logs remain in `artifacts/path-coverage-probe-{build,metal}.log` in the ProGPU
worktree and the parent workspace's
`artifacts/native-windows-consumer.akdIwM/path-coverage-{normal,warp}.log`.
Hosted [diagnostic run 34768876404](https://github.com/wieslawsoltes/ProGPU/actions/runs/34768876404)
now passes on both Windows x64 and ARM64 using the original failing package
3000, with exact expected raw/atlas/untouched samples on Microsoft Basic Render
Driver. This narrows the basic raster/partial-copy path, not cubic geometry,
native binding preparation or fragment sampling. The original package gate
remains failed.

The independent `Native path diagnostics` workflow reuses a specified completed
Build's package and logs its run number, original head and package hashes. It is
manual-only and requires an explicit run ID: PR pushes must not repeatedly
evaluate a historical failing package as though it were their current artifact.
The Build workflow now runs all three independent stages against its own package
after a Windows consumer failure, preserving that failure and all final gates.
The manual workflow is not a current-head artifact producer or replacement gate. It invokes
`--native-path-probe` (cold direct native rectangle) and `--native-cubic-probe`
(the unchanged original MIL cubic assertion), each in a fresh process. Metal
passes the direct rectangle with exact white/black RGBA samples. The workflow
collects all stage exits and still fails if any probe fails; no first-frame retry
or hidden warm-up is involved.

The completed [three-stage run 34769078839](https://github.com/wieslawsoltes/ProGPU/actions/runs/34769078839)
passes all stages on hosted ARM64. On hosted x64, coverage/copy passes but the
cold direct native rectangle and the original MIL cubic frame are both black.
The x64 failure therefore does not require MIL lowering or cubic geometry. The
same package's x64 binaries pass both native probes under x64 .NET in Parallels
with its normal display adapter. This is not a controlled driver/cache comparison
or qualification of the full x64 package consumer. Fresh processes do not imply
an empty driver shader cache.

## Demand-driven native path pipelines

### Current-package result and vector-stage isolation

Build [34770390199](https://github.com/wieslawsoltes/ProGPU/actions/runs/34770390199),
head `5a3b6bfd`, package `0.1.0-preview.3005.ci`, finishes with only the two
Windows package consumers failing. All six native renderer lanes, the browser,
strict compiler checks and the four non-Windows package consumers pass. Both
Windows consumers still lose the original first-frame rectangle and cubic ink.
The independent coverage/copy probe passes on both; the direct native rectangle
fails on x64 and passes on ARM64, while the independent cubic probe fails on both.
Demand-driven pipeline creation therefore did not repair the hosted black frame.

`--path-vector-probe` extends the existing diagnostic with a manually populated
canonical 56-byte vector vertex, 224-byte frame uniform and 256-byte solid brush.
It uses the unchanged packaged `Vector.wgsl` `vs_main`/`fs_main_unmasked` entries
to sample the independently rasterized R8 atlas. Both renderers use that shader;
this tests their shared shader/binding contract independently of native resource
preparation. The first draw is submitted before raw/atlas readback, without
warm-up or a CPU synchronization used to make the atlas visible. It checks exact
white interior and opaque-black exterior, then the original coverage/copy checks.
This is test-only manually prepared input, not a new renderer or qualification
substitute. Local Release compilation has zero warnings/errors and the Metal run
passes all samples. The manual workflow reuses an explicitly selected package;
Build failure diagnostics use their own package. Original consumer failures,
query checks, deadlines and final qualification gates are unchanged.

The completed [vector-stage run 34773037544](https://github.com/wieslawsoltes/ProGPU/actions/runs/34773037544)
passes canonical coverage, partial copy and vector shading on both Windows
architectures using package 3005. The x64 native rectangle loses its device at
readback and the cubic remains black; ARM64's native rectangle and cubic pass
in this diagnostic run, unlike its original consumer. These separate processes
do not isolate driver/disk cache history or establish deterministic cold behavior.
The normal first-frame gate remains failed. An independent VM software-adapter
run also passes canonical vector shading with the original package 3000 runtime.

`--path-vector-batched-probe` performs the same exact coverage/copy/vector work
in one encoder and one queue submission, matching the direct native batch shape.
The separate-submission probe remains available to distinguish the two contracts.
Neither variant reads the atlas before drawing, changes shaders or retries a
failed frame. Metal passes the single-submission variant with exact samples.

The next diagnostic uses `--path-native-layout-probe`: the actual native direct
rectangle's 40x16 padded tile, `(2,2)` atlas origin, nonzero solid-brush index,
64-KiB retained buffers and its complete vertex/index/brush payload. The explicit
scalar diagnostic byte hash and native `CapturePayloadHash` both report
`37CF2B2338D40B07` on Metal; both render exact white/black samples. Native probes
print preparation metrics before waiting so device loss does not erase that
evidence. This is diagnostic parity of a fixed payload, not a production CPU
renderer or evidence of shader/compiler/resource-lifetime equivalence on Windows.

Run [34773803547](https://github.com/wieslawsoltes/ProGPU/actions/runs/34773803547)
passes that reference native-layout draw on both Windows architectures, with the
same `37CF2B2338D40B07` payload hash as the native producer. Nevertheless both
native direct rectangles are black; the original cubic subsequently loses the
x64 device and exits with an access violation on ARM64. The payload comparison
rules out different vertex/index/brush preparation for this fixed case, not
native GPU resource contents or lifetime. Earlier ARM64 diagnostic successes
are not deterministic current-consumer qualification.

Two additional separate-process stages mirror native GPU setup:
`--path-native-bindings-probe` uses the exact explicit common vector layouts,
visibility and minimum sizes, releasing caller pipeline-layout ownership after
pipeline creation as native code does. `--path-native-submit-probe` additionally
uses the existing `wgpuQueueSubmitForIndex` and exact-token `wgpuDevicePoll`
extension ABI before readback. Neither changes production submission, dependency
selection, shaders, sampling or assertions. Both compile without warnings/errors
and pass on Metal, with the original payload hash and exact samples; Windows
results remain required. Diagnostic raw extension calls are not new public APIs.

`ProGPU.Wpf.ShowcaseApp` first-path startup currently creates seven coverage
pipelines, including three signed-winding shader modules, even for an ordinary
rectangle. Both C++ providers now retain common atlas/layout resources but create
only pipeline families requested by actual nonempty raster batches. Ordinary,
inline signed, split leaf, Boolean combine and staged signed work retain their
original entry points, bindings and execution order. Staged signed work creates
its existing leaf/evaluate/pack trio together. Paths and clip paths share this
engine-owned cache; later requests can add missing families. Pipeline failure
returns before a caller-owned encoder is consumed, with no renderer fallback.

This removes six unused pipeline creations and three unused modules from an
ordinary-path-only engine. It does not change raster math, sampling, draw bounds,
submission count, atlas allocation/eviction, owner input, worker scheduling or
the existing engine/device lifetime. It is not a claim that compilation cost
caused either the hosted black frame or browser readback timeout, and no measured
latency improvement is claimed yet.

Local validation uses rebuilt libraries: both native providers compile, all 20
C++ tests pass, and fresh Metal direct-path/cubic probes pass without warm-up.
Existing managed/native comparison runs pass ordinary paths, forced inline and
staged signed paths, and vector clip chains. Those comparison runs retain their
existing warm-up and pixel tolerances, so only the separate cold probes provide
first-frame evidence. Final-head Windows/browser/package gates remain required.

### Cross-engine design review

The review uses public contracts, not foreign implementation code. The existing
[query pipeline review](native-mil-query-pipeline-specialization.md) remains the
shared engine-owned resource precedent. The following references bound this
change; they do not establish equivalent driver behavior or benchmark results.

| Family | Public contract and decision |
| --- | --- |
| Skia / SkParagraph | [SkPath](https://api.skia.org/classSkPath.html) and [Paragraph](https://github.com/google/skia/blob/main/modules/skparagraph/include/Paragraph.h): retain ProGPU geometry and text ownership; no scene, layout or font cache rewrite. |
| Direct2D / DirectWrite / Win2D | [ID2D1Geometry](https://learn.microsoft.com/en-us/windows/win32/api/d2d1/nn-d2d1-id2d1geometry), [HitTestPoint](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextlayout-hittestpoint), [CanvasGeometry](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_Geometry_CanvasGeometry.htm): preserve geometry/input semantics independently of deferred GPU resource construction. |
| WebRender | [Rendering overview](https://firefox-source-docs.mozilla.org/gfx/RenderingOverview.html): keep retained scene/input and GPU execution responsibilities separate; no new scene flattening or invalidation policy. |
| Vello / Parley | [Scene](https://docs.rs/vello/latest/vello/struct.Scene.html) and [Layout](https://docs.rs/parley/latest/parley/struct.Layout.html): preserve existing ProGPU scene/layout boundaries, not a new composer. |
| HarfBuzz | [Shape plans](https://harfbuzz.github.io/harfbuzz-hb-shape-plan.html): no shaping-plan, glyph, fallback-font or text cache changes. |

The adopted technique is demand-driven creation in ProGPU's existing engine,
bounded by actual batch requirements. DPI/transform keys, culling, allocation
limits, eviction, precision and SIMD policies remain unchanged. No dependency
or foreign source was added.

## Research boundary

The upstream [wgpu driver issue inventory](https://github.com/gfx-rs/wgpu/wiki/Known-Driver-Issues)
distinguishes historical WARP descriptor-lifetime faults from AMD partial-texture
update faults. [Issue 1306](https://github.com/gfx-rs/wgpu/issues/1306) records the
latter behavior; [issue 1002](https://github.com/gfx-rs/wgpu/issues/1002) records
the former. These motivate isolating observable transfer behavior, not a claim
that an old driver defect causes this failure. No foreign implementation was
copied, and no speculative full-atlas clear, driver replacement or descriptor
workaround is introduced.
