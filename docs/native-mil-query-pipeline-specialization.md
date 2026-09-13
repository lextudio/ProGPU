# Native MIL query pipeline specialization

## Core acceptance and evidence

The Showcase's pointer input and geometric selection must execute native queries
over the presented source owner snapshot. Windows package-consumer validation
also requires point, rectangle and ellipse queries, including source point-only
and region-only participation. None of those contracts is deferred here.

The first point specialization at 532a95ea passes the Windows VM's cubic and
retained-render fixtures and submits its first query after 44,723.421 ms. A later
live `dotnet-stack` snapshot of that same process reports `BeginHitTest` beneath
the owner-snapshot fixture, not a readback wait. The earlier general-query runs
were retired before first submission. This is evidence for continuing query
compilation work, not a controlled benchmark or final Windows qualification.

## Shared algorithm, separately compiled query families

`GpuHitTesting.wgsl` retains one quadtree traversal, source primitive/clip policies,
ordering/deduplication, counters and result writer. Its entry points select
constant query families:

| Entry point | Accepted request | Classification |
| --- | --- | --- |
| `cs_point` | Point | Existing exact point predicates |
| `cs_bounds` | Rectangle region | Existing rectangle intersection detail |
| `cs_ellipse` | Ellipse region | Existing ellipse intersection detail |
| `cs_main` | General reference | Original request-selected behavior |

Wrong-family requests return without publishing a hit. Product consumers already
validate their request flags and select the matching entry point. Constant family
arguments permit the shader compiler to discard unrelated classification paths;
there is no duplicated traversal, simplified geometry, readback-based CPU query,
changed compiler default or increased readback deadline.

Managed queries cache one pipeline per requested family in the existing device
cache. Native providers first prepare their shared shader, layout, buffers and
index; they then lazily create the requested pipeline in a fixed three-slot
engine-owned array. A rectangle/ellipse-first engine does not compile point input
as a prerequisite. Resource release clears every populated slot. Browser readback
packing keeps its separate existing entry point and lifetime.

The query algorithm's asymptotic work, 64-entry traversal stack, candidate buffers,
owner-generation binding and stable upload policy are unchanged. Maximum retained
query-pipeline storage increases from two to three pointers/pipelines per engine;
ordinary point-only operation still creates just one. Shader compilation latency
must be measured separately from submission/readback and stable replay; successful
Metal results do not establish FXC or software-adapter performance.

## Validation and remaining merge requirements

All 124 managed GPU hit-test tests pass locally. The new first-family theory
starts with each family, checks exact owners/intersection details, verifies one
new cached pipeline per requested family, and checks point/list reuse afterward.
Both native providers compile. The rebuilt Metal native consumer passes original
owner/generation/participation checks plus fresh rectangle-first and ellipse-first
engines followed by point queries. Native generated contracts verify.

Windows MSVC built both providers after renaming the pipeline-release loop local
to avoid shadowing the engine's existing `pipeline` member under `/WX`. The VM's
full-family consumer passes cubic/retained rendering and submits its first point
query in 29,963.419 ms; subsequent point/topmost and point/list submissions take
1.710/0.729 ms. Rectangle pipeline creation is still pending. These are diagnostic
observations, not controlled benchmarks or a completed Windows gate.

The earlier point-only process ultimately terminated with 0xC0000005 after
18m42s. Windows Error Reporting names `coreclr.dll` as the fault module; the
managed stack ends in native `BeginHitTest`. Slow compilation is observed, but
neither the compiler nor an interop lifetime defect is established as the crash
cause. Do not describe this as a diagnosed FXC crash.

The full-family head passes Linux build/tests. The earlier WinUI cache failure
also passes on the subsequent diagnostic head without any weakened assertion;
its intermittent cause is not established. The browser gate reaches evidence
readback but times out at `map-requested`, with no reported browser error.
Final-head CI and exact package gates remain required. Windows CI's independent
cubic rectangle-ink failure also remains open. See
[Windows investigation](native-windows-package-consumer-investigation-2026-09-13.md).

## Shared four-sample path classification

The family-only Windows comparison subsequently terminated with 0xC0000005 after
7m34s while creating the first rectangle pipeline. Separating families is not a
complete repair. Inspection of the existing ProGPU shader found four separate
calls to the complete sampled-path walker for rectangle corners, plus four
cardinal calls for ellipse regions; each walker evaluates the same curves.

The canonical walker now carries four independent boundary/parity/winding lanes
and evaluates each segment/curve sample once. Rectangle corners occupy those
lanes; ellipse cardinal samples use the same batch after the existing boundary
and center checks. Single points splat one coordinate and consume lane x. There
is no second scalar algorithm, new geometry approximation, HLSL fork, changed
fill rule, tolerance, curve-step count, source clip or index lifetime.
Boundary hits remain sticky independently per lane. Horizontal edges never
divide by zero. The shader adds constant four-lane private state, not buffers,
dispatches, retained allocations or CPU readback. Asymptotic O(S) path work is
unchanged; rectangle sample evaluation is one curve traversal rather than four.
Actual compiler optimization and point-query cost remain platform measurements.

Validation: all 156 focused hit/shader tests pass, including eight new cases for
both fill rules, every reflected corner order, reversed winding, mixed boundary
and interior samples, and exact point-versus-region results. Both native providers
compile on macOS and Windows. The rebuilt Metal native consumer exits 0. The
Windows comparison passes rendering and submits the first point query in
19,818.134 ms; rectangle completion is still required. No final Windows performance
or crash-resolution claim follows from the earlier point submission.

The preceding committed head be199695 passes the browser gate; the earlier
readback timeout remains an undiagnosed intermittent failure, not a changed
deadline. Its System.Drawing lane separately reports 7,296 warmed enumeration
allocation bytes against the unchanged 4,096-byte limit. Three superseded Build
runs (34762600632, 34761948474, 34761357934) were cancelled to release runners;
cancelled runs are not passing qualification.

### Design references and provenance

This is an original refactoring of ProGPU's existing edge predicates and fixed
curve evaluator. [WGSL vector semantics](https://www.w3.org/TR/WGSL/#vector-types)
permit independent sample lanes; [HLSL loop documentation](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-for)
explains that loop rolling/unrolling is a compiler decision, not a portable WGSL
promise. No compiler behavior is assumed as proven by source size alone.

The [input ownership research](native-mil-hit-test-ownership.md#design-references-and-decisions)
was revisited against public WebRender, Skia/SkParagraph, Direct2D/DirectWrite,
Win2D, Vello/Parley and HarfBuzz documentation. Retain independent spatial/query
metadata and reusable shaping/layout/scene state. Reject raster alpha or source
bounds as replacement geometry. Lazy family pipelines, source culling, path/
glyph/texture cache keys, eviction, demand-driven upload, worker preparation,
DPI/subpixel/hinting, fallback/variable fonts and device-loss generation ownership
remain unchanged. Only independent samples within one existing GPU query are
batched; no external implementation source is copied.
