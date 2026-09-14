# Native source geometry clip input

## Concrete acceptance dependency

LibreWPF's existing `NativeMilGeometryRelationSmoke` source-built host fixture
sets the drawing visual clip to `M8,8 L88,8 8,88 Z`, then selects inside/outside
that triangle. The visual paints a larger rectangle. Native MIL represents
the real clip with a vector mask, but index capture previously rejected every
mask state. A successful source geometry utility query is not native index proof.

This batch connects that actual clip path, including source visual and
DrawingContext PushClip ingress. It does not enable native host queries or claim
all clip/mask combinations are implemented.

## Contract and implementation

The public C++ builder's `add_vector_clip_mask` overloads have an optional
`source_geometry_clip` annotation, false by default. It declares original source
input geometry, not material/raster coverage. Annotated masks require unit opacity.
The flag is builder-owned metadata: no stream layout, C ABI or shader changes.
MIL's `append_geometry_clip` and rotated rectangle clip path supply it. Other
vector masks do not gain source semantics simply because their data looks similar.

The hit producer admits a declared single intersect path with no boolean program
under explicit source-geometry capture. Line, quadratic and cubic segments retain
their kinds and control points, fill rule and closure. Their own clip transform
maps them once into world coordinates, independently of the drawing transform;
existing NEON/SSE2 four-coordinate work is shared with primitive bounds mapping.
The canonical query shader receives the actual segment range, not an envelope
containment substitute. Clip bounds only prune candidates.

Each used mask's transformed segments are cached once per index build and shared
by affected hit primitives. Cache allocation is lazy; scenes without vector masks
do not allocate it. This adds O(R + S) storage and O(R + S + P) work for resource
slots R, distinct used clip segments S and affected primitives P. State/save/restore
and source-owner boundaries remain unchanged. Partial/unsupported capture never
publishes a successful index. There are no per-primitive native calls, GPU
submissions, raster readbacks or new compute fallback paths.

A containing rectangular clip is redundant and keeps the real vector clip.
An exact four-line rectangular vector path can also intersect a nonredundant
rectangle. Admission proves closure, four unique bounds corners and nonzero
axis-aligned edges after the clip transform; bounds alone never prove topology.
Both winding directions, rotated starting vertices, axis reflection and quarter
turns work. Curves, disconnected edges, repeated corners, bow ties and shears
do not acquire rectangular semantics. The existing intrinsic rectangle
intersection produces one shared four-edge range per state/layer/frame scope.

Declared rectangular geometry masks on source identity-effect layers likewise
become final source clips, intersected with enclosing composite clips. They do
not contribute shadow padding, effect allocation bounds or opacity-mask coverage
to input. Save/layer exit restores the previous clip. Undeclared effect masks
are rejected at recording; nonrectangular masks remain rejected by complete input
capture. MIL records this metadata before effect raster lowering, preserving
actual source ownership and cached content frames.

Nonrectangular nonredundant intersections, multiple vector paths, boolean clip
programs, arcs, spatial opacity masks and undeclared material masks remain
explicit unsupported native-input contracts. Do not overwrite a path clip with
rectangle segments or combine independent contours as if winding meant
intersection. Cache-boundary masks remain independently guarded. Generic
rendered-visibility capture continues rejecting mask states.

## Paired applicability and provenance

The managed source visual path already uses `PushSourceGeometryClip`,
`PushGeometryClip`, `PathAtlas.CompilePath` and canonical `WithClip` payloads.
It needs no duplicate algorithm; a paired triangle fixture retains transformed
edges, two draws sharing the clip and restored unmasked sibling input at zero
source opacity. Native scenes 9820/9821 cover the same triangle, actual MIL
ingress, range reuse/restoration and rejected declaration/composition cases.
Scene 9822 checks all polynomial control lanes against an explicit scalar affine
oracle and retains even-odd fill. An import consumer covers the C++ overload.

Original ProGPU implementation sources: native builder resource recording,
`place_primitive` intrinsic mapping, `GpuRenderCommandHitTestCache.cs` and
`GpuHitTesting.wgsl`. No third-party implementation was ported. The
[existing cross-engine input design references](native-mil-hit-test-ownership.md#design-references-and-decisions)
remain applicable: preserve source spatial/clip metadata separately from raster
coverage, with scene-qualified owners. Shaping, font discovery, retained upload,
shader workgroups, worker scheduling and device ownership are unchanged.

The MIL coverage digest is regenerated after source decoder edits. Fixtures are
authored for final execution, not evidence of runtime correctness or performance.
Native provider/module, managed renderer/headless, source/package applications,
Windows comparisons, lifetime/performance and both PR CI gates remain required.

## Showcase popup follow-up — 2026-09-14

The actual package-derived Showcase native-input probe exposed a popup vector
rectangle `(0,0)-(173,74)` intersected with client clip `(3,3)-(170,71)`, followed
by a separate drop-shadow popup with an output geometry mask. The native producer
rejected these compositions despite their exact rectangular topology. Scene
10801 now covers both state and effect placement, shared ranges, clipped bounds,
disjoint clips, restoration and explicit invalid/undeclared-mask rejection.
Canonical MIL fixtures cover rectangular PathGeometry clips around blur, zero
blur and shadow, point-only children, source edits and unmasked siblings.

Both native providers compile and all 19 CTest suites pass on macOS ARM64.
The live diagnostic overlay advances past the previous compilation failures but
is not a passing package/application gate: its follow-up is GPU query completion.
Final-head package, Windows/Linux and full managed/headless gates remain required.

This is a native index-producer integration gap, not a change in shared query or
managed source clipping semantics. Existing managed source clip/effect traversal
already preserves these scopes; its paired fixtures remain authoritative. The
implementation reuses original ProGPU `transform_hit_coordinates` and
`intersect_hit_clips`; no foreign source, ABI, shader or fallback is introduced.
Rectangle recognition is four dependency-bound topology checks once per used
clip/frame; independent coordinate/intersection lanes retain NEON/SSE2. No new
performance claim follows from passing correctness tests.
