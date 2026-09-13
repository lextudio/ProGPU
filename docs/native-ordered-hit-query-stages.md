# Ordered GPU hit-query stages

## Scope and status

Acceptance application: **ProGPU.Wpf.ShowcaseApp**. User action: pointer selection
and geometry-region queries against a presented native MIL owner index. The
blocking path is the Windows system-WARP execution of the canonical query shader.
This work provides a standalone differential probe and optional shader entrypoints;
it does **not** switch the managed or C++ product dispatcher to staged execution.
The existing six-binding point/bounds/ellipse entrypoints remain available.

The staged probe now passes all **120 complete result buffers** on system ARM64
WARP with the verified DXC configuration, matching the independent Metal/original
shader reference. This closes the isolated staged-query differential, not the
product dispatcher, package or Showcase qualification gates.

The implementation is original ProGPU code, refactored from
`src/ProGPU.Vector/Shaders/GpuHitTesting.wgsl` at `20bbf7f1`. No foreign renderer
implementation was copied. Both product providers embed that canonical file.
The staged dispatcher in `tests/ProGPU.HitQuery.ContractTests` is native-WebGPU
test code, not a third renderer or CPU geometry fallback.

## Contract

1. A shared resumable BVH iterator emits candidates in the original node/local
   reference/LIFO-child order, preserving the 64-entry stack and query filtering.
2. A GPU clip pass uses the original world-space clip predicates. It does not
   clear clip metadata: that metadata still controls precise-region admission
   and whether a primitive can fully contain the query.
3. Independent 64-lane primitive-family passes use the original exact query
   predicates, including the existing curve sampling, caps and tolerances.
4. A single invocation merges surviving candidates in original traversal order,
   using the shared ordered owner insertion logic. Summary participation and
   traversal counters are reduced locally and published explicitly.

Grouping complete queries by primitive family was rejected: equal-depth overwrite,
duplicate-owner retention, insertion and full-list rejection depend on original
order. Sorting by depth alone does not reproduce those rules.

Internal binding 7 stores a 32-byte header and eight-byte candidate records. The
header contains count, overflow and a three-word indirect dispatch at offset 16.
A GPU copy transfers those 12 dispatch bytes to a separate indirect buffer;
writable storage and indirect reads must not alias in a dispatch usage scope.
Normal probe execution uses one submission and no intermediate CPU readback.
`--trace-stages` deliberately submits and waits after each stage for fault
isolation; its timings are not normal-path performance measurements.

The legacy entrypoints cannot reach candidate storage through their call graph.
A constant false argument alone was insufficient for automatic layout inference;
the shared iterator keeps traversal reuse without adding a seventh legacy binding.
The probe also executes actual `GpuHitTestEngine` calls to check the six-binding
product layout, rather than only its own explicit staged layout.

For N primitives, K candidates, S path segments and result capacity R, average work
is O(log N + K*(S+R)), worst-case O(N*(S+R)). Staged scratch is O(M) for M retained
index references; classification uses nine fixed family passes plus clipping.
The original 1/16/24-piece stroke walks now share segment sampling and one
intersection call site per query shape. Evaluation order, first/last cap ownership,
rational weights, unknown-segment exclusion and point endpoint tests are unchanged.

## Reproduction and evidence

```sh
dotnet run --project tests/ProGPU.HitQuery.ContractTests -c Release -- \
  --reference-shader /absolute/original-20bbf7f1.wgsl \
  --write-reference /absolute/new/reference.bin
dotnet run --project tests/ProGPU.HitQuery.ContractTests -c Release -- \
  --expected-results /absolute/reference.bin
```

On Windows, use the existing verified DXC feature/compiler configuration and an
isolated apphost, then pass `--software-adapter --staged-only --expected-results`
with the independently captured reference. Staged-only execution cannot publish
its own reference. References are created without overwriting existing files,
and every byte of all 17 result slots is checked, including counters and unused
slots. Missing/truncated/trailing reference data and candidate overflow fail.

The expanded fixture has 160 interleaved primitives, duplicate owners, equal and
mixed depths, all eight defined primitive kinds plus an unknown-kind control,
curved clips, point/region-only and invisible records, transformed strokes, all
cap kinds, line/quadratic/cubic/arc/rational/unknown/degenerate segment cases.
It checks three query shapes, four capacities and ten locations: **120 queries**.
macOS Metal matches the pre-stroke-refactor reference byte-for-byte, as well as
the original shader at `20bbf7f1` and current legacy/staged GPU implementations.
CI fetches that exact baseline shader for the macOS/Linux differential and retains
the reference artifacts; existing Windows/native package gates remain unchanged.
The smaller 60-query reference
also qualified the shared iterator before stroke sampling was consolidated.

Reference SHA-256:

- 60-query reference: `345a7f19e76fcca47819b2753251c6375ac9f8b3221d1d9e27505c0dafb66bcb`.
- 120-query pre-stroke reference: `d89271ad04fb1d944f86fa5352f37b360955e52d79f58dcccc3e8955c5653275`.

System ARM64 WARP first completed all 20 original point cases, then crashed in
the bounds path-stroke stage. Separating clips alone did not fix that crash.
Consolidated stroke sampling completed that stage and all 40 expanded point cases;
the first bounds comparison then exposed a precise-test counter difference
(expected 107, actual 139), with matching owner/intersection output. Moving counters
to local state alone did not resolve that Windows difference. The gate remains
strict; passing hits without passing counters is not qualification.

Counting returned candidates outside the resumable iterator's nested loops
resolved the difference: system WARP then completed all 120 comparisons, including
every counter and unused result slot, with exit 0. The matched run used one normal
submission per query, not the per-stage diagnostic waits. It used DXC 1.8.2502.8,
the pinned feature-enabled wgpu-native binary and system WARP 10.0.26100.9278.
The entire count path remains on the GPU. The deliberately undersized one-record
candidate buffer also fails explicitly in the macOS negative probe.

This dense fixture primarily exercises references retained in the root node;
multi-level traversal, sparse/multi-subtree hits and maximum device capacities
still require dedicated coverage before a staged product default is admitted.

The existing monolithic query still crashed on this system runtime after the
stroke refactor. Do not infer a product repair from the staged probe. Exact loaded
compiler, native dependency and system WARP paths were observed; no testing-only
WARP was bundled and no system files or runtime defaults were changed.

The full `ShaderResourceTests`/`GpuHitTestingTests` project could not build in the
recovered source-only checkout because its pinned ACadSharp and WinUI theme
submodules are absent. An initial `--no-restore` invocation produced no tests and
is not counted as a pass. The independent contract project has no such dependency.

## Required before product admission

- Extend the passing system ARM64/Metal differential to x64, Linux, multi-level
  BVHs and device limits. Retain original deadlines/fences and exact counters.
- Pair managed and C++ dispatcher/resource lifetime changes; add the typed shared
  indirect operation for native, Dawn and browser providers. Do not bypass a
  borrowed device through the native-only API used by this diagnostic probe.
- Size scratch from the validated immutable index generation, reject overflow
  before publishing results, preserve pending-readback/owner-map leases, and
  respect device storage/indirect-workgroup limits with bounded dispatching.
- Compile only required pipelines for actual retained primitive families, retain
  device-qualified cache ownership and measure cold/warm query latency, allocation
  and residency against the same final legacy/native binaries.
- Finish compiler-feature packaging and final native package/Showcase input,
  popup/DPI/lifetime gates. No dependency pin or merge advances from this probe.

## Research and design decisions

The cross-engine research matrix in
[native input ownership](native-mil-hit-test-ownership.md#design-references-and-decisions)
was rechecked for this work: [WebRender](https://firefox-source-docs.mozilla.org/gfx/RenderingOverview.html),
[Skia paths](https://api.skia.org/classSkPath.html),
[SkParagraph](https://github.com/google/skia/blob/main/modules/skparagraph/include/Paragraph.h),
[Direct2D](https://learn.microsoft.com/en-us/windows/win32/api/d2d1/nn-d2d1-id2d1geometry),
[DirectWrite](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextlayout-hittestpoint),
[Win2D](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_Geometry_CanvasGeometry.htm),
[Vello](https://docs.rs/vello/latest/vello/struct.Scene.html),
[Parley](https://docs.rs/parley/latest/parley/struct.Layout.html), and
[HarfBuzz](https://harfbuzz.github.io/harfbuzz-hb-shape-plan.html).
Retained layout/scene reuse, broad-phase culling, lazy pipelines and batched GPU
work are retained; no font, DPI, hinting, atlas-generation or owner policy changes.
Worker preparation and device-qualified caches remain the existing product owners.
The [WebGPU usage model](https://gpuweb.github.io/gpuweb/#programming-model-resource-usages)
informs the separate indirect buffer and dispatch dependencies. The pinned
[wgpu DX12 compiler configuration](https://github.com/gfx-rs/wgpu/blob/87576b72b37c6b78b41104eb25fc31893af94092/wgpu-hal/src/dx12/shader_compilation.rs)
was inspected only to understand compiler options; upstream code was not modified
or imported into this implementation.
