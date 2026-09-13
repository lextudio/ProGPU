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

Windows execution of this full split, final-head CI and exact package gates remain
required. Windows CI's independent cubic rectangle-ink failure and Linux's WinUI
stable-cache assertion also remain open. See
[Windows investigation](native-windows-package-consumer-investigation-2026-09-13.md).
