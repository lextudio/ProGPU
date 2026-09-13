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
  With the same isolated development WARP, its first point query and all 16 waits
  pass. Bounds submission takes 245,917.082 ms versus the unchanged fixture's
  341,098.475 ms; ellipse submission takes 62,207.937 ms versus 69,585.134 ms.
  The full fixture now exits 0, including fresh region-first contexts. These
  are encouraging diagnostic observations, not controlled benchmark results:
  the baseline and candidate processes overlapped for part of the run. Both
  observed cold latencies remain unacceptable for normal application input.

The unchanged development-WARP fixture has now completed successfully in full.
System WARP still fails its matched apphost control; changing rectangle-edge
classification does not establish a repair of its separate point-query crash.

## Corner containment batching

The next bounded batch keeps the same acceptance action and research decisions.
Rectangle corners now share `points_in_triangle4`: one pair of vector calls
replaces eight scalar triangle calls in a quad, and one vector call replaces four
scalar calls in a triangle cap. The scalar point entry splats its coordinate into
that same helper and consumes lane x; there is no second production predicate.
Preserve the exact edge cross-product/sign algorithm, including inclusive
boundaries, both windings and the original degenerate-triangle behavior. No
geometric shortcut, tolerance change, new array, buffer, dispatch or CPU fallback
is introduced. Source provenance remains the original ProGPU predicate at
`a1409ae8`; the shader's O(1) local-work/storage and overall query bounds persist.

The GPU differential now checks 4,096 segment/triangle lane comparisons and the
single-point wrapper. All 158 focused tests and 20 native tests pass. Both native
providers compile on macOS/MSVC; the full hash-checked Metal consumer passes.
Metal native DLL SHA256:
`5b5a8f55cdac3f8e4d0efb78a5447e26722e4a1bae2680197b53346c8f7d8300`.
Windows native DLL SHA256:
`e696e6a9809f82d5baa5d45c3fcabbf11a19d7888e6c05a7cc663f3893337d52`.

The system-WARP control still fails with `0xC0000005`, after 25,457.121 ms point
submission and before first readback. Development WARP passes point/repeated
waits and all participation checks; observed bounds submission is 216,465.624 ms
and ellipse submission 78,582.698 ms. Fresh region-first contexts remain live.
The mixed timings are not a controlled or all-family performance improvement;
system-runtime compatibility and cold latency remain unqualified.

The package fixture now logs actual loaded Windows WARP/compiler/D3D12Core
versions and paths before query timing, and separates region submission from
readback milestones. This diagnoses the terminal repair Build `34781150565`:
ARM64 rendering passes but querying exits 127 after first submission; x64 passes
point work, submits a bounds query in 144,479.732 ms, then stays inside its wait
until job cancellation. That is not merely a still-compiling ellipse pipeline.
Neither the existing wait contract nor the job/readback deadlines are widened.

## Independent compiler/runtime comparison

The already built exact-ABI wgpu-native DXC variant is now tested against this
same corner-batched shader in fresh owner-query processes. It changes only the
optional compiler feature/explicit compiler selection and controlled WARP DLL,
not geometry, native query lifetime or assertions. The observed modules confirm
the intended feature-enabled wgpu-native, DXC/DXIL 1.8.2502.11 and WARP paths.

With development WARP 1.0.20 the **entire owner fixture passes**, including fresh
region-first contexts. First point submission takes 985.959 ms, bounds 1,478.972
ms and ellipse 1,023.666 ms. These are diagnostic observations, not a controlled
throughput benchmark, but they identify a much larger compiler-dependent cost
than the local predicate expansion. The matched system-WARP process submits its
point query in 743.437 ms, then exits `0xC0000005` before first readback.

Consequently compiler cost and system runtime execution are separate blockers.
Do not describe the older DXC failure as proof that DXC cannot execute this query,
or the new development-runtime pass as a system compatibility repair. The shipped
wgpu-native still lacks the optional DXC feature; no production dependency,
compiler default, SDK admission, assertion or timeout is changed by these probes.
The next compiler work must preserve the exact ABI, reproducible native feature
build and compiler provenance, configurable policy and paired renderer gates.
Testing-only WARP remains excluded from product distribution.

The DXC-capable dependency SHA256 is
`52b3eea2261daabc3980e9690cc66b1bcce5eada451808882504e300f12db84d`;
DXC SHA256 is `07b524496b69582d324589d1ff1b2a1c84fc082f496fff4da873ed2ee7948f20`;
DXIL SHA256 is `acf694bf193ecdc5cd51f20305df18fcc5844426fc3fc930cc100066719d8b7b`.
Both native libraries and the isolated diagnostic managed selection are tracked
in the local Windows artifact scripts, not copied into source or packages.

Do not promote compilation, first submission or development-WARP success into
system-runtime or application qualification. Current-head CI, Windows system
compatibility, cold/warm latency and full application/platform gates remain
required. Testing-only WARP binaries are not distributed with the product.
