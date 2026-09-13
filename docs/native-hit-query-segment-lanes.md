# Four-lane rectangle-edge queries

## Application blocker and bounded change

Acceptance application: `ProGPU.Wpf.ShowcaseApp`; action: native rectangular
source selection. The shared region shader expands four calls to the same
segment-crossing predicate per rectangle. Those calls recur inside stroke-body,
cap and path tests. On isolated development WARP the preceding full shader takes
341 seconds to create its first bounds pipeline. System WARP separately crashes
in point execution; this change does not claim to repair that driver failure.

`GpuHitTesting.wgsl` now batches the four rectangle edges in one `vec4` predicate.
This is original ProGPU source refactoring: provenance is the scalar
`segments_intersect` and `segment_intersects_rect` at `a1409ae8`. No external
implementation is copied. Both managed and C++ providers embed the same shader.

The vector lanes preserve cross-product expression order, inclusive endpoint
tests, parallel threshold `0.000001`, collinearity threshold `0.0001`, and the
original collinear bounds-overlap result. Parallel lanes select a safe divisor
because WGSL evaluates both select operands; those lanes still return only the
collinear result. `any` combines completed independent edge results, not partial
predicate conditions. The early endpoint-inside test remains unchanged.

There are no additional buffers, dispatches, readbacks, native crossings, shader
families or private arrays. Work remains O(1) per rectangle/segment pair with
constant four-lane private state. Native compiler expansion is reduced from four
scalar call sites to one vector call site; actual machine code and latency are
compiler-dependent and must be measured. BVH O(N*S) worst-case work, its 64-entry
stack, source ownership, clipping, intersection details and result ordering are
unchanged. This is GPU work, not a new CPU fallback.

## Research and retained architecture

The design rechecks the primary sources in
[input ownership research](native-mil-hit-test-ownership.md#design-references-and-decisions):
WebRender retained rendering, Skia/SkParagraph geometry/layout, Direct2D geometry,
DirectWrite point metrics, Win2D geometry, Vello scenes, Parley layout, and
HarfBuzz shaping plans. Those sources support preserving the separation of
geometry, retained scenes and source text semantics; none supplies implementation
code for this change. [WGSL vector semantics](https://www.w3.org/TR/WGSL/#vector-types)
permit independent arithmetic/mask lanes without requiring hardware SIMD width.

Adopt batching only within the existing GPU query. Retain lazy family compilation,
scene/layout reuse, source culling, glyph/path/texture keys and eviction,
demand-driven upload, worker preparation, GPU submission organization, DPI,
subpixel/hinting policy, fallback/variable-font identity and device-loss generation
handling. Reject raster-alpha queries, CPU geometry substitution, eager unrelated
pipelines, reduced path families and cache invalidation changes. This is not a
new renderer/startup architecture or a claim that those unrelated systems improved.

## Validation

- All 157 focused hit-test and shader-resource tests pass on Metal.
- The new differential fixture executes 2,048 independent GPU lane comparisons
  against the original ProGPU scalar predicate, retained only in a test shader.
  It covers randomized crossings/misses and targeted collinear, parallel,
  zero-length, endpoint, reversed and near-threshold pairs. Sentinel outputs
  reject unwritten lanes, and the fixture requires both true and false results.
- Both native providers compile; all 20 native CTest entries pass, including
  Dawn/WebScene execution. The full rebuilt Metal native package consumer passes
  rendering, owner/generation/participation and independent region-first checks.
  Build and consumer native DLL hashes match:
  `32744beaa414db7bdeaf9917403d42e13aa1de367f299b30472b4d7c2c8315bc`.
- MSVC builds both providers. The staged ARM64 DLL has SHA256
  `b027e83ef793b9fd6d337119aadb51f93b62427bed4239aa0c71fbee6e528795`.
  Its full owner fixture is running with the same isolated development WARP
  as the unchanged baseline; final status and comparative latency remain open.

Do not promote compilation, first submission or development-WARP success into
system-runtime or application qualification. Current-head CI, Windows system
compatibility, cold/warm latency and full application/platform gates remain
required. Testing-only WARP binaries are not distributed with the product.
