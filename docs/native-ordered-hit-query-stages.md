# Ordered GPU hit-query stages

## Scope and status

Acceptance application: **ProGPU.Wpf.ShowcaseApp**. User action: pointer selection
and geometry-region queries against a presented native MIL owner index. The
blocking path is the Windows system-WARP execution of the canonical query shader.
The managed and C++ product dispatchers now support explicitly selected ordered
stages, using retained device/index-owned resources. Automatic selection retains
single-pass queries pending final platform/application qualification. The existing
six-binding point/bounds/ellipse entrypoints remain available and independent.

The staged probe now passes all **120 complete result buffers** on system ARM64
WARP with the verified DXC configuration, matching the independent Metal/original
shader reference. This closes the isolated staged-query differential, not the
final package/default or Showcase qualification gates. The full native consumer
also passes with the product dispatcher on Metal and system ARM64 WARP/DXC;
the Windows package-production/default configuration remains a separate gate.

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
Without a forced execution policy, the probe also executes actual
`GpuHitTestEngine` calls to check the six-binding product layout. `--product`
requires explicit ordered selection and compares public product queries too.

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
- Qualify the paired product dispatchers (below) on the final package/host graph,
  including failure recovery, pending owner-map leases and large admitted device
  capacities. Oversized indices currently reject rather than chunking dispatches.
- Measure cold/warm query latency, allocation and residency against the same
  final legacy/native binaries; retained family selection alone is not a benchmark.
- Finish compiler-feature packaging and final native package/Showcase input,
  popup/DPI/lifetime gates. No dependency pin or merge advances from this probe.

## Shared indirect dispatch — 2026-09-14

`IWebGpuApi.ComputePassEncoderDispatchWorkgroupsIndirect` now forwards the original
compute-pass/buffer identity and 64-bit byte offset through Silk and Dawn. The
browser adds opcode 54 with a 16-byte payload (pass u32, buffer u32, offset u64);
its producer and actual JavaScript decoder reject offsets beyond JavaScript's
exact integer range instead of rounding. Existing packet opcodes/layouts and
version remain unchanged. Buffer bounds, alignment, usages and device ownership
remain WebGPU validation, not CPU argument readback. The C++ Dawn provider resolves
the same procedure through its current device dispatch scope; missing procedures
reject provider initialization. Borrowed devices never enter a native-only API.

The original ProGPU shared API/protocol and procedure-table patterns are the
implementation provenance; no third-party implementation was imported. The
[WebGPU indirect-dispatch contract](https://gpuweb.github.io/gpuweb/#dom-gpucomputepassencoder-dispatchworkgroupsindirect)
defines three u32 arguments in a 12-byte indirect buffer and a four-byte-aligned
offset. Each forwarded command is constant-time with no argument readback or
geometry work; browser packet storage is fixed-size and shared packet batching
is unchanged. GPU buffers/pipelines remain owned by the existing context and
submission lifetime; this API creates no resource or separate queue submission.

Validation:

- All 120 complete original-shader query buffers match using the shared API on
  wgpu-native Metal and Dawn Metal (`--dawn`). Both also match the independently
  recorded pre-refactor reference. The Dawn test borrows `DawnGpuContext.Context`
  and disposes the owning Dawn context once, after query resources.
- The Windows ARM64 system-WARP rerun passes the same 120 independent reference
  comparisons through the shared API (exit 0, DXC). The isolated
  `C:\ProGPU.OrderedQueryStages-System-Strokes-SharedApi` run observed the actual
  staged wgpu-native DLL, pinned compiler pair and system `d3d10warp.dll`; no
  development WARP or system replacement was used. This remains the diagnostic
  dispatcher, not the native package consumer.
- Eleven actual `BrowserWebGpuApiTests` pass in a focused linked test project
  (six new cases). The full repository test project remains dependent on the
  absent source submodules; this focused run is not a full-suite pass.
- `node --experimental-vm-modules eng/progpu-test-browser-indirect-dispatch.mjs`
  executes the actual browser decoder with test resources: four exact offsets,
  two inexact offsets, two malformed payloads and one stale handle. This is
  transport testing, not real-browser device qualification.
- The native provider forwarding fixture compiles with strict C++20/Apple Clang
  against pinned Dawn headers and passes exact handle/u64 forwarding, missing-
  procedure rejection and dispatch-scope restoration. It is also called by the
  full native Dawn contract executable; standalone success is not a full native
  renderer build.
- CI includes browser decoder checks and the independent Dawn/Metal differential,
  in addition to the existing native provider and original-shader query gates.

This shared-API checkpoint preceded the product connection below.

## Paired product dispatch — 2026-09-14

Managed hosts configure `WgpuContext.HitTestExecutionPreference` with
`Automatic`, `SinglePass` or `OrderedStages` before resource construction.
`PROGPU_HIT_TEST_EXECUTION=auto|single-pass|ordered-stages` supplies the process
default; invalid values fail. `HitTestExecutionPath` reports the resolved path.
Automatic currently resolves to SinglePass: the new path is not silently promoted
from an isolated passing probe. Shared surfaces inherit their actual device
owner's selection and limit snapshot. Raw C hosts opt in using the generated
`PROGPU_NATIVE_ENGINE_ORDERED_HIT_QUERIES` flag (32); old native libraries reject
the unsupported flag rather than selecting another path.

`GpuOrderedHitQueries` belongs to one retained `GpuHitTestDeviceIndex`. It leases
the common layout from the same device resource domain and uses a retained
pipeline cache, compiling only collection, merge, the requested clip shape and
the actual retained primitive families. It retains separate single/list bindings,
an M-record candidate buffer and a 12-byte indirect buffer. Disposing the index
queues the usual completion-safe resource releases. No shader/pipeline/bind-group
creation or index upload repeats for warm queries of an already requested family.

The C++ dispatcher uses the same canonical entrypoints, family mask and ordered
passes. Candidate/argument buffers follow its existing immutable index generation;
pipeline resources follow engine/device lifetime. Index replacement publishes all
new resources together, and failed allocation releases only unpublished resources.
Existing pending token/map, owner snapshot, query completion and browser packing
contracts remain in place. Query dispatch remains one submission; native desktop
readback retains its separate submission for the existing Vulkan hazard contract.

Both sides validate actual storage and compute limits before admission. Native
queries read the owning device limits through its provider. Managed owned devices
capture limits at initialization; Dawn and browser factories publish their actual
device snapshot through immutable `WgpuComputeLimits`. Other borrowed hosts must
provide that snapshot—default/zero limits do not grant staged admission. Existing
external initialization method signatures are preserved. The candidate buffer is
`32 + 8*M` bytes; limits cover buffer/storage size, seven storage bindings, 64 X
invocations and `ceil(M/64)` workgroups. Oversized indices explicitly reject, with
no scalar/managed geometry fallback and no readback to calculate dispatch sizes.

If GPU collection overflows, merge publishes the reserved UINT_MAX summary hit
marker through the ordinary result readback. Admitted M is strictly below that
value, so valid hit counts cannot collide. Both product readers reject the marker
before copying any owner results to the caller; native map/token state is released
as an explicit failed query. This avoids another readback/map lifetime.

### Product evidence and remaining limits

- The full project-reference native consumer passes on Metal and system ARM64
  WARP with DXC, including real retained MIL pixels, native owner/generation
  isolation, repeated waits, participation and region-first queries. The loaded
  native product DLL hash on Windows is
  `16490f220019d0f1125349bc6ec2b1a10e11a1765529d5be75f0a787148235d5`.
  The Windows stdout hash is
  `fd6ad3e6bacb62772714e9eb51ed40385aaafec3bfb84a06b953ff62a9cd2eea`.
  These are staged project-reference binaries, not proof of the final NuGet graph.
- The matched system-WARP/FXC run fails during compute pipeline creation with
  D3DCompile X3511 (forced loop unrolling failed). The pinned wgpu error conversion
  subsequently aborts on a NUL in that diagnostic. No owner-query submission or
  readback completed in this run. Splitting dispatch therefore does not qualify
  FXC: the next Windows release blocker is the DXC-capable dependency/compiler
  package graph, followed by exact packaged runtime and adapter qualification.
- The dense probe matches 120 complete buffers plus 100 public managed product
  queries on wgpu-native Metal, Dawn Metal and system ARM64 WARP/DXC. Public list
  APIs do not execute zero-capacity queries; single-point summary is separately
  covered. Public output comparison retains untouched caller-tail records and
  the managed API's existing -Infinity empty-depth initialization (the raw native
  probe uses -FLT_MAX). No other result fields or tolerances are normalized.
- The sparse fixture adds root-local coverage and separated child subtrees.
  All 168 complete buffers and 140 public queries match the original shader on
  Dawn Metal, current single-pass Metal and staged Metal. The independent original
  Dawn reference SHA-256 is
  `12e78381009b7417e38f04fd88f6f4d6b54f302b0f091a50d756956a1a658530`.
  Historical `20bbf7f1` through wgpu-native Metal reports `nodes_visited=1` where
  Dawn's original shader and the new local reduction report 11 or 13. That old
  compiler-path discrepancy is retained explicitly; counters are not ignored.
  CI cross-checks complete sparse outputs against Dawn's original shader, rather
  than treating the broken old Metal counter as authoritative or waiving it.
- Both C++ providers build on Apple Clang and Windows ARM64 MSVC; 19 native tests,
  20 focused managed policy/browser tests, nine actual browser-decoder checks and
  generated native contract verification pass. The full managed test checkout
  still requires its missing external source submodules.
- Linux/macOS native package lanes now run the full consumer with explicit
  ordered stages in addition to their unchanged default and NativeAOT gates.
  Windows keeps its existing gates; the DXC-enabled package gate still needs the
  feature-capable dependency payload and cannot use the stock FXC-only binary.
- Large-capacity runtime, sparse Windows/x64, browser device execution, exact final
  package CI, qualified automatic selection and Showcase source/runtime gates
  remain required. VM configuration/system libraries were not changed; WARP is
  not redistributed. CPU work here is one dependency-free fixed family-mask
  reduction over strided metadata per index, not geometry or a compute fallback.

This connection changes no font/layout, clip geometry, owner identity, ordering,
sampling quality or source input policy. It shares original ProGPU traversal and
predicates; the cross-engine and WebGPU research decisions above remain applicable.

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
