# Single-path raster pipeline specialization

## Contract and provenance

The authoritative algorithm is ProGPU's existing
`PathRasterizer.wgsl::ordinary_path_coverage_byte` with `pathOpKind == 0`.
The specialized `cs_main_single_path` entry uses the same analytic winding,
fill rules, sample grids, scale/phase, rounding, packed output and tails. It
does not traverse a second operand or compile the Boolean expression stack.
Nonzero operation kinds do not write through this specialized entry.

Managed `PathAtlas` classifies every admitted request in its existing preparation
loop. An all-single-path batch selects the new lazy device-cached pipeline;
mixed and Boolean batches keep `cs_main_ordinary`. The first item, bounds, or
source geometry type cannot establish the batch policy. Empty or rejected
batches compile neither pipeline. Both variants share the existing shader and
binding leases, and are disposed with their existing cache owner.

Both C++ providers use the same canonical shader. Native path and retained-vector
clip execution classify the actual unsplit uniform batch while preparing it.
Mixed inline batches retain the ordinary/signed pipeline; split Boolean and
signed-winding stages, submission order and lifetime remain unchanged. The new
pipeline is engine-owned, lazily created and released with the engine. No C ABI,
renderer/compiler/adapter default, workgroup size, atlas key/generation, culling,
upload, scene ownership, scratch capacity or readback deadline changes.

Classification adds O(1) work per already-visited request and O(1) batch state.
Single-path raster work remains O(A*S) per pixel for A samples and S segment
visits, with O(1) private state. There is at most one additional cached pipeline
per device/engine; unchanged frames create no pipeline or new upload.

## Independent checks and remaining qualification

`SinglePathRasterPipelineTests` compares every raw output word from the original
and specialized GPU entries across 128 combinations of six segment kinds,
ordinary/doubled/degenerate figures, both fill rules, four sample grids and two
placements/scales. It checks row padding, intercase guards, partial-word tails,
overdispatch and rejected operation kinds. Two actual-atlas sequences cover
simple-first and Boolean-first compilation, mixed batches, retention and disposal.
The lightweight Windows executable links these three original test bodies,
three additional linear-path bodies and the signed-linear regression, requiring all seven to execute without
skip/discovery substitutions. Windows CI
executes them in independent processes, each still bounded by 120 seconds. Every
case has an exact execution marker and its own logs; a final receipt requires all
seven successes. The default executable mode executes all seven together.

## Proven linear-path batches

`cs_main_linear_path` shares ProGPU's original winding, sample-grid, fill-rule,
scale, phase and packing implementation from commit
`77eaecc49f1b1194fb6f63e9ac3000646f703643`. A constant admission parameter removes
curve solvers from this entry's reachable program; the general ordinary and
signed/Boolean entries retain the complete original segment evaluator. No external
implementation text is included and no curves are approximated by straight edges.

Managed preparation checks every admitted request's segment kinds and operation
before choosing the linear pipeline. Both native providers perform the same
kind classification during their existing full source-segment validation loops,
for both geometry frames and retained vector clips. Native selection is
conservative: even an unused curved source segment keeps the full kernel.
This single-path entry excludes every Boolean operation. The separate signed
linear entry described below preserves signed/Boolean dispatch semantics. A first item,
bounding rectangle, geometry name or endpoint pair cannot establish admission.

The raw linear GPU entry also rejects every non-line/unknown kind, nonzero
operation and invalid segment slice before publishing coverage. It preserves
four-pixel word ownership, zero partial-word bytes and untouched caller padding.
There is one additional lazily owned pipeline, with the original shader/bindings,
atlas keys/generations, upload/submission order, cache invalidation and teardown.
Managed classification costs O(S) for new raster work; native classification is
O(1) per already-visited source segment. The kernel adds O(S) kind reads per word
to the original O(A*S) work, retaining O(1) private state. Idle retained frames do
not scan segments or create pipelines. No ABI, compiler/adapter defaults, sample
quality, fallback policy or completion deadlines change.

`LinearPathRasterPipelineTests` adds 512 exact complete-buffer GPU comparisons
against the full single-path entry: both fill rules; rectangle, doubled,
degenerate and crossing outlines; all four sample grids; fractional placement
and scale; widths 0/1/2/3/4/7/16/17; row padding and overdispatch. Separate raw
controls reject a later segment of each curve/unknown kind and operation bits.
Two actual-atlas sequences prove line-first and curve-first lazy creation,
later-curve mixed-batch admission, retained pixels, Boolean separation and disposal.
The original 128-case general-path differential and both original atlas tests
remain mandatory; each of the six Windows bodies retains its 120-second bound.

The motivating original Forms grid diagnostic observed 11 admitted paths with
44 line segments and no curves. Independent Windows ARM64 D3D12/automatic-FXC
pipeline observations (original/linear/linear/original) were
5082.463/350.480/246.142/5572.288 ms. These are API wall times, not statistical
GPU performance, a cold driver-cache benchmark or application acceptance.
The grid still requires its original native-input/startup package gate. Full
native package/clip/Boolean gates, Windows x64/ARM64, browser and image checks
remain required; no result here closes the separate WPF ARM64 pending-GPU issue.

Local Metal Release evidence: 43 focused regressions passed, zero skipped; the
standalone harness also executed all three bodies. One observation reported
157.292 ms ordinary and 46.145 ms single-path pipeline acquisition. These are
API wall times, not GPU latency, statistics, an isolated cold-cache comparison,
Windows evidence, or proof that the Forms grid starts within its deadline.

The first Windows x64 combined process passed all three original bodies and
recorded 22939.613 ms ordinary / 3649.803 ms single-path API acquisition. The ARM64
combined process exceeded 120 seconds after logging 38185.829 ms / 9312.365 ms;
its final regression body was not identified and it is a failure, not a partial
qualification. This prompted independent bounded case execution, not a longer
per-process deadline or removal of any case. Neither observation is a matched
application benchmark. The mixed-atlas regression additionally requires a simple
request before a Boolean request, proving the batch cannot select from its first
item alone.

Required before merge: full Build/Docs and Svg.Skia parity, Windows x64/ARM64 raw
GPU differentials, native path/clip/Boolean package coverage for both providers,
and representative matched startup/performance evidence. Existing native package
selectors, independent cases, exact image inventories and process deadlines stay
intact. The native WPF resize/idle failure is not declared fixed by this change.

## Proven linear signed-winding batches

The native `cs_main_linear_winding` entry specializes the original ProGPU
`PathRasterizer.wgsl` and `PathRasterizerCommon.wgsl` from
`ac25d7c8c8fdeb7cf8967d83f3e8f50850fd4287`. It propagates the existing constant
linear admission parameter through ordinary, Boolean and signed row evaluation.
Every original line crossing, sample, scale, phase, postfix operation and packed
output remains GPU work. No external implementation source was used.

Both native providers select it only when the existing complete source-segment
validation finds exclusively line segments and the actual inline batch contains
a signed program. A later unused curve closes admission. Geometry frames and
retained vector clips use the same rule, lazy engine-owned pipeline and normal
engine teardown. The raw shader independently checks the entire bound segment
inventory before writing output; native bindings expose the actual source byte
extent rather than buffer allocation capacity. Ordinary mixed requests in that
same batch retain their own original operation. Curved signed batches still use
the general kernel; existing staged policy, leaf/evaluate/pack pipelines and
single-path operation-zero gate are unchanged.

Managed applicability was audited in `PathAtlas`: it emits ordinary Boolean
predicates and selects `cs_main_ordinary`, not the native signed-program format.
Its ordinary/single/linear dispatch therefore remains unchanged. It and both
native providers consume the same canonical shader resources. The linked managed
raw GPU regression exercises the new signed entry directly without inventing a
second signed source transport.

Host classification adds no pass or allocation: it reuses the existing O(S)
source validation. Raw admission adds O(S) kind reads per four-pixel output word;
raster work remains O(A*(S+N)) per pixel for A samples, S segment visits and N
bounded postfix instructions, with O(D) private stack storage for D <= 16. There
is at most one additional lazy pipeline per engine. Workgroups remain 16 by 16;
bindings, uploads, submissions, retained identities, completion deadlines,
adapter/compiler defaults, ABI and fallback policy do not change.

The independent region regression exposed an existing final-predicate error in
the general signed evaluator: it interpreted a nonzero outer group's signed sum
as EvenOdd. The shared final predicate now tests Nonzero, matching the staged
evaluator. Even signed magnitudes such as 16 must remain filled. The original
Microsoft WPF oracle confirms this for sixteen clockwise and sixteen
counterclockwise contours, while preserving its original cancellation and
CombinedGeometry cases. No source contour or expected pixel was changed to
accommodate the correction.

`LinearWindingRasterPipelineTests` checks 1,040 cases against independent source
region formulas: all five binary Boolean operations, signed sums and negation,
cancellation, raw versus predicate even-odd contours, empty tokens, depth 16,
four sample grids, fractional phase/scale, five widths, complete output words
and untouched row/intercase padding. Six appended unused curve/unknown kinds
must leave the entire output untouched. The standalone `linear-winding` case
accepts explicit `fallback` or `reference` modes for software-adapter or general
kernel comparison; normal CI retains all six previous cases and adds the new
case with the same independent 120-second process bound.

Local Windows ARM64 D3D12/automatic-FXC evidence on Microsoft Basic Render Driver:
all 1,040 cases and six rejection controls pass, as does the unchanged complete
native sample with its original scene, font and pixel checks (67.323 seconds).
The prior general shader crashed on the exact captured original GPU inputs;
all four line-specialized captured buffers, including their untouched tails,
match the original Metal general shader byte-for-byte (94,720 bytes each).
Physical M3 Metal also passes all 1,040 regions through both the specialized
and general signed entries, and all 49 native tests across both provider aggregates. Its complete original native sample
remains byte-identical (SHA-256
`d746b309d74ce695640cba22b0cfa7372ec8dab8b468ce38c2930154b2a7e5d7`).

These are local source and sample results, not whole-producer/package or complete
Windows qualification. The preexisting software-adapter GPU hit-query deferral
remains explicit in the original sample. Curved signed programs, forced execution
policies, Windows x64/ARM64 CI, final packages and source applications retain their
own gates. The separate Intel macOS exact UNORM mismatch remains unresolved.

## Architecture research

These primary references inform boundaries only; no third-party implementation
text or control flow is copied. The implementation is specialized original ProGPU
code, not a translation of another engine.

| Reference | Adopted or preserved boundary |
| --- | --- |
| [Skia/SkParagraph Paragraph](https://github.com/google/skia/blob/main/modules/skparagraph/include/Paragraph.h) | Preserve shaped/layout results separately from painting; this change does not invalidate paragraphs or alter font/geometry caches. |
| [Direct2D performance](https://learn.microsoft.com/en-us/windows/win32/direct2d/improving-direct2d-performance), [DirectWrite layout](https://learn.microsoft.com/en-us/windows/win32/directwrite/text-formatting-and-layout) | Reuse device resources and batch work; keep layout and rendering separate. No extra flush or atlas reallocation. |
| [Win2D deferred resources](https://microsoft.github.io/Win2D/WinUI3/html/LoadingResourcesOutsideCreateResources.htm) | Demand-driven work must retain device-loss ownership. No unowned asynchronous warm-up is introduced. |
| [WebRender overview](https://firefox-source-docs.mozilla.org/gfx/RenderingOverview.html) | Preserve retained display-list, visibility and GPU execution boundaries; do not rebuild scenes to choose a kernel. |
| [Vello](https://github.com/linebender/vello), [Parley](https://github.com/linebender/parley) | Retain GPU raster work and CPU text layout as separate responsibilities; reject a CPU raster fallback. |
| [HarfBuzz responsibilities](https://harfbuzz.github.io/what-does-harfbuzz-do.html) | Shaping remains outside this raster optimization; fallback, variable fonts, hinting, DPI and subpixel policy are unchanged. |

The changed dimension is demand-driven pipeline selection. Worker preparation,
scrolling/retained-scene reuse, visibility, glyph/path eviction, device generation,
text/fallback and demand-driven upload keep their existing ProGPU contracts.
