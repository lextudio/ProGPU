# Native retained dashed-stroke input

## Core application dependency

After empty-union clip reduction, ProGPU.Wpf.ToolkitApp's AvalonDock auto-hide
outline reaches a closed, four-point native stroke with width 1, two dash
intervals and WPF joins. Rendering already supports that stroke. Recorded input
previously rejected any nonzero dash interval count, preventing the complete
native scene/index from being published.

## Implementation and provenance

The scene hit-index builder now consumes ProGPU's existing
`make_semantic_polyline`, `make_semantic_dash_style`, `try_create_dash_pattern`
and `walk_dashed_polyline`. The implementation sources are
`src/ProGPU.Native/src/Scene/progpu_native_semantic_stroke.hpp` and
`src/ProGPU.Native/src/Backend/progpu_native_geometry_dash.hpp`.
This preserves normalized phase, negative offsets, doubled odd patterns, dash
runs crossing corners, original endpoint caps and closed-seam joins.

Visible run bodies use the existing line-stroke query primitive with flat body
ends. Separate caps and joins use the renderer's existing
`create_cap_triangles` / `create_join_triangles` and the original source owner,
clip and active input frame. Flat, square, triangle and round caps are retained.
Round caps keep the native renderer's eight-triangle half-circle construction;
this does not claim an ideal analytic circle or tighter managed/native boundary
error than the existing native stroke geometry. Conformal and general affine
join/cap domains match native rendering. Raster AA fringes are never input.

Aligned typed point/dash scratch is allocated only when dashed input occurs and
reused across the capture. Bulk copies use intrinsic `memcpy`, preserving actual
batch point/double offsets. The phase traversal has sequential dependencies and
is shared with rendering; bound/segment transforms retain the existing intrinsic
kernels. This is retained CPU metadata construction, not CPU pixel rendering or
a readback fallback. Cost is O(input points + intervals + emitted run/cap/join
geometry), bounded by the existing native index limits. No benchmark speedup is
claimed.

Managed applicability was checked in `GpuRenderCommandHitTestCache`:
`TryAddLinearDashCoverage` and `TryGetDashedStrokePath` already emit dashed input
from actual retained geometry. Its production path needs no new dash algorithm.
The paired `DashedSourceInputTests` compares the same four width-2 [2,1] runs,
including a nonuniform shear/translation, without initializing a GPU.

## Qualification — 2026-09-14

Native scene 9850 tests all four caps, open/closed contours, identity/general
affine transforms, actual dash bodies, cap/join counts, inherited clipping and
following-owner restoration (16 combinations). Scene 9851 adds independently
tabulated fractional/negative phase and odd patterns with two descriptors in
one payload, checking nonzero point/double offsets. The complete native build
(including both providers) and all 19 CTest suites pass, as do generated contract,
MIL coverage, native memory inventory and Unicode verifiers.
The paired managed tests pass in the focused source project, not the full managed
suite. Final exact-package device queries and platform qualification remain
required; these fixtures are not a complete application pass.

The unchanged diagnostic Toolkit now passes auto-hide and overview lifecycle.
Its next failure is a timeout waiting for the floating editor's distinct
presented host. The subsequent teardown also reports a native recursive-mutex
error. Neither the separate-host/device-index requirement nor the deadline was
changed. Investigate host creation/presentation and teardown; do not substitute
model-only Float/Dock checks or owner-window input. The evidence is
`toolkit-diagnostic-auto-hide-dashed-input.log` in the external core-release
diagnostic directory, not final NuGet qualification.

Device-width/hairline strokes, splines and other unadmitted native input
contracts remain explicitly guarded. Broader goal requirements remain tracked
separately from this core application correction.
