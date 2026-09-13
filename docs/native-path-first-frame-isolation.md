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
not a current-head artifact producer or replacement gate. Its follow-up invokes
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
