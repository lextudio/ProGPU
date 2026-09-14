# Suntrail and the reusable game engine

Current integration note (2026-09-14): the 2.5D sample, optional world pass,
shared instances, artwork renderer and spatial hierarchy compile and pass the
Suntrail Release suite; the opt-in world/instance paths remain disabled in
ordinary play. A signed iOS Release build and Browser AOT publish have passed,
but this exact revision has not been installed or profiled on the unavailable
iPhone. Historical "not run" statements below refer to their original coding
batches. The [delivery plan](suntrail-plan.md) lists remaining phone, format and
3D gates.

Managed/native applicability: `ProGPU.GameEngine`'s material pages, spatial
index and collision helpers are new managed library contracts used by this
game. `RasterArtwork.wgsl` and `Suntrail.wgsl` are canonical game shaders
shared by its Desktop, iOS and Browser hosts. The native C++ renderer has no
Suntrail scene, asset or gameplay model and no matching per-game drawing
extension. The only `ProGPU.Scene` core change makes existing projection and
active-opacity getters public for typed managed extensions; their calculations,
native ABI, GPU commands and C++ renderer semantics are unchanged. A future
native game adapter would require paired resource/output tests before shipping.

The immediate engine scope is the rendering cost Suntrail actually has. `ProGPU.GameEngine`
is a separate library depending on `ProGPU.Backend`, not WinUI or the sample. Its first
consumer is Suntrail's public drawing extension. This is a foundation in development,
not a completed general-purpose engine or a claim of AAA quality.

## Evidence and design decision

The September 5 iPhone workload spends roughly 30–34 ms in fragment work while managed
simulation takes about 0.05 ms. World shader specialization helped nominal-temperature
runs but did not preserve the frame-rate gain in the warm run. Generating rock cells,
foliage, mountain shading and noise for every covered pixel on every frame is the
architectural cost to remove. Another hash optimization or a large ECS rewrite does
not address that evidence.

The new flow is:

1. Game data produces visible instances, retaining painter order.
2. A material compiler requests only visible regions of immutable procedural artwork.
3. Engine residency pins existing requests, reserves bounded misses, and evicts only
   pages not used by the current frame. Generation checks reject stale handles.
4. The GPU compiles misses into material pages using the original canonical Suntrail
   artwork functions. A page becomes usable only after its bake submission.
5. Frame rendering samples resident artwork and applies current light emitters, tint,
   clipping and vignette. Water, characters, articulated foliage and effects remain live.
   Tree wind is the same affine sway, applied to cached quad vertices.

## Implemented boundary

`MaterialPageCache<TKey>` is a typed, fixed-capacity CPU residency table. The key belongs
to the material author. Lookups are expected O(1); a miss scans at most the fixed slot
count. Two-phase pinning prevents an early miss from evicting a later visible request.
Reserved pages cannot be evicted. Commit/cancel make bake readiness explicit.

`MaterialPageAtlas` owns device-local GPU storage and gutter/UV geometry. Suntrail selects a 4992-square
RGBA16Float allocation costing 190.125 MiB (under a 192 MiB budget), with 1444 independent 128-square interiors and
one-texel gutters. It never grows, performs no readback, and cannot cross device domains.
`MaterialPageInstance` is a contiguous, immutable 96-byte instanced transport.
An experimental shared-source transport is described below; the measured installed
build still uses the expanded 96-byte records.

Suntrail owns material classification, immutable recipe keys, its canonical shader,
world-space light behavior, and the procedural compiler adapter. Keys include original
size, material parameters/seed, world, dungeon, physical texel density and page coordinate.
Camera movement, local lights and tint do not invalidate static appearance. Changed
recipes/DPI request different pages. Device recreation creates a new atlas and table.
The Suntrail adapter currently owns one active game view per compositor; multi-view scene rendering is a later engine boundary. The WinUI compositor continues owning UI text, layout, glyph caches and presentation.

The initial 128 MiB candidate left 117 visible pages uncached on the device workload.
The final bounded setting fits that live set with zero visible fallbacks. Measured median iPhone fragment time is 16.51 ms versus 30.60 ms for the direct reference; full-frame pacing still misses the 60 FPS goal. See [validation](suntrail-validation.md).
The library accepts an explicit atlas extent; the game owns its platform memory choice.

Compilation is capped at 32 pages per preparation. Missing pages render through the
original live material equations; no geometry is silently dropped. All resident pages
share one texture binding. Contiguous cached runs combine different materials in one draw;
dynamic runs retain the existing bounded shader variants. Normal replay allocates no
managed objects and does not upload unchanged page instances or rebake resident pages.
First-use pipeline creation, cache convergence and scrolling misses must be measured
separately from settled replay. This first implementation does not yet provide level-load
prewarming or predictive page requests.

## Quality and limits

This replaces analytic material evaluation with at-least-native-density, filtered material assets;
it is intentionally not a bit-identical screenshot cache. The original material generator,
world differences and directional shading remain. Cliff normals now use a fixed half-world-unit
height stencil instead of screen derivatives: texture assets must not bake camera-phase-dependent
lighting. This refactors the original height field without replacing its rock/soil materials. Storage retains HDR values in float16;
there is no resolution scale reduction. At 1x display DPI, materials compile at 2x density to protect fine grain; Retina uses native density. Linear sampling and material-space AA can change
subpixel edges compared with evaluating analytic AA at each translated screen pixel.
Focused all-world images record mean/RMS error, outlier coverage and visible seams before
enabling this path. The direct analytic path remains available for comparison.

There are no mipmaps or perspective minification in this first orthographic consumer.
Full 3D must add a material representation with normals/roughness, mip generation,
perspective sampling, mesh geometry, depth, camera controls and volumetric collision.
It must not reinterpret this 2D appearance cache as a complete physically based 3D material.

## Primary research and clean-room provenance

Only public architecture/behavior is adopted. No third-party implementation was copied.
All artwork equations originate in this repository's
`src/ProGPU.Samples.Suntrail/Shaders/Suntrail.wgsl` at `977aa449`; the material/compiler
split directly refactors that original ProGPU source.

- [Unreal runtime virtual texturing](https://dev.epicgames.com/documentation/en-us/unreal-engine/runtime-virtual-texturing-in-unreal-engine): adopt cached, camera-independent material appearance and bounded updates. Adapt to a small deterministic CPU-visible page set; reject GPU feedback tables, compressed virtual-texture streaming and enormous world infrastructure for this workload. Animated appearance stays live.
- [Unity 6 SRP Batcher](https://docs.unity3d.com/6000.0/Documentation/Manual/SRPBatcher.html): adopt persistent GPU material data and contiguous per-instance buffers. Avoid exploding the shader-variant count for each asset. This is not a direct port of Unity batching.
- [Godot GPU optimization](https://docs.godotengine.org/en/stable/tutorials/performance/gpu_optimization.html): texture/material reuse and batching inform a common page binding; visibility granularity remains small instead of combining the whole level into one uncullable mesh.
- [Direct2D performance](https://learn.microsoft.com/en-us/windows/win32/direct2d/improving-direct2d-performance) and [Win2D offscreen drawing](https://microsoft.github.io/Win2D/WinUI3/html/Offscreen.htm): reusable GPU resources have an explicit owner; cached content needs explicit initialization, alpha and invalidation rules. No D2D/Win2D implementation or platform bridge is introduced.
- [WebRender](https://github.com/servo/webrender): retain the visibility/preparation/render separation already recorded in the Suntrail design research. The earlier decision to avoid artwork texture caches is superseded by measured iPhone fragment cost.
- [Vello](https://github.com/linebender/vello): retain a separate CPU scene and GPU work stage. A general compute vector rasterizer is not the needed replacement for these fixed artwork functions.
- [Skia text architecture](https://skia.org/docs/dev/design/text_overview/), [DirectWrite](https://learn.microsoft.com/en-us/windows/win32/directwrite/programming-guide), [Parley](https://github.com/linebender/parley), and [HarfBuzz shape plans](https://harfbuzz.github.io/shaping-and-shape-plans.html): preserve CPU shaping/layout reuse, font fallback and existing text caches. This change does not introduce a second font engine, alter DPI/subpixel rules for UI text, or move shaping to the GPU.

Startup, lazy pipelines, scene reuse, culling, texture residency, demand-driven writes,
batching, DPI and device ownership are directly applicable. Worker preparation is deferred
until profiling shows CPU recipe work is significant. Glyph eviction, fallback fonts,
variable-font state and shaping are unchanged and remain in ProGPU's existing text stack.

## Managed/native applicability

This library consumes the existing public managed WebGPU backend and is shared unchanged
by Desktop, iOS and Browser. Suntrail currently has no native C++ scene/game host, so there
is no second game material implementation to port. The core managed/native compositor,
wire ABI, atlas algorithms and text shaders are unchanged. Any future C++ game host must
consume the same canonical artwork shader and matched page/quality contract; a generic
core rendering change would require paired managed/native implementation and regressions.

## Remaining engine work in task order

First finish material-page image/performance gates, residency tuning and level-load
preparation, then install the optimized iPhone build. Next introduce only the shared
scene/chunk and asset identities needed by connected levels and editor invalidation;
extract stable simulation timing/input actions when the actual second consumer needs
those boundaries. Complete connected rooms, Mario-compatible import/export and authoring.
Full 3D is last: perspective camera, actual meshes/depth, material channels/mips, lighting,
3D movement/collision and editor controls. Keep Suntrail rules/content outside the engine.


## Contact and timed-support simulation (implementation batch)

The engine now also owns `ContactSurface` and `TimedSupportState`. These small typed
value records describe character motion along a contact tangent and a contact-triggered
warning/absence cycle. Collision detection, contact basis, character controls, surface
coefficients and level content remain game-owned. A tangent scalar is independent of
whether the caller has a 2D axis or a 3D contact plane; this does not provide a 3D solver.
Both operations are O(1), use no delegates, reflection, wall-clock time or managed
allocation, and timed supports use integer simulation ticks. One bounded state array
is created with each room and reset on respawn. Rendering only reads that state.

Primary behaviour references are the [Box2D simulation documentation](https://box2d.org/documentation/md_simulation.html)
(contact friction, restitution, tangent speed, and fixed-step separation) and
[Godot PhysicsMaterial](https://docs.godotengine.org/en/stable/classes/class_physicsmaterial.html)
(surface-specific friction and bounce). Adopt distinct surface response parameters;
adapt them to Suntrail's existing kinematic character controller. Reject a general
rigid-body solver/ECS migration for this change: it is not required by the current
contact behaviours and does not address the measured rendering bottleneck. The
implementation and procedural art are original ProGPU code, with no external source
port. The existing in-repository controller remains the collision authority.

Suntrail supplies spring launch speed, ice acceleration/braking, conveyor speed and
a 66-tick warning followed by 300 absent ticks at 120 Hz. Springs launch without a
held jump; holding jump adds a modest boost. Conveyor transport preserves a stationary
collision shape. Collapsing surfaces retain their timer through room travel, cannot
be kept alive by repeated contact, and recover after their absence interval.
The canonical Suntrail shader adds original metal coils, moving belt treads, ice
veins and widening stone cracks. Spring/ice artwork is material-cache eligible;
belts and collapse warnings remain live. Shader work is bounded to five coil turns
or constant-cost expressions and one sprite per surface.

All three existing hosts consume the same managed game simulation and canonical
shader. There is no native C++ game host/simulation counterpart to update; the core
managed/native rendering contract and C ABI are unchanged. Tests for these semantics
have been added but not run, consistent with the requested implementation-first pass.
Shader resource audits, output-quality review, campaign reachability, FPS and all-host
validation remain pending. The earlier device measurements describe the earlier
material-renderer revision, not this expanded gameplay workload.


## Game-owned depth pass (implementation batch, opt-in)

`SceneRenderTarget` adds an engine-owned color/resolve, optional MSAA color, and
Depth32Float allocation with an explicit byte ceiling. It retains exact physical
resolution and the host's 1x/4x sample count. A request above the ceiling is refused;
Suntrail uses its existing painter renderer in that case. It does not lower resolution,
sample count, alpha precision, or procedural detail to make a request fit. Stable
attachment reuse is O(1); resize releases old logical ownership before allocation.
Actual in-flight residency can temporarily include earlier submitted attachments
until backend deferred disposal completes and must be measured separately.

Suntrail's opt-in pass divides cached material fragments by **exact alpha one**:

1. One front-to-back instanced draw reads the existing 96-byte page buffer through
   vertex storage. It uses reverse instance indices, with no reversed CPU copy,
   index upload or per-page draw. Missing/live pages and non-opaque tints contribute
   no opaque geometry. Exact opaque texels write color and depth without blending.
2. Original painter-order runs draw translucent cached texels and all live/fallback
   material functions. They test against opaque depth without changing that depth.
   Alpha edges, clouds, articulated foliage, actors, particles and water retain their
   existing functions. Page depth strictly follows the original painter order.
3. The same public WinUI drawing extension composes the resolved scene as one quad,
   using one unfiltered texel load per output pixel. Menus, controls and UI text remain
   compositor-owned. No core project/API edits are required by this consumer.

This is an opaque color pass followed by transparency, not an alpha-threshold foliage
cutout. It avoids shading fully hidden material pixels where depth rejection is
profitable. A fixed scene can reuse its completed scene texture; changed batch or page
upload generations, target generation and rendering flags cause redraw. The shader
reads at most the populated prefix of the fixed 8192-record buffer. Existing material
misses retain live fallback. The source classifier and draw count never drop a page.

The render-target budget is 192 MiB **in addition to** the material atlas budget.
At 2796×1290 and 4x MSAA, declared attachment storage is 129,846,240 bytes
(123.83 MiB): resolved RGBA/BGRA8, four-sample color and four-sample Depth32Float.
Transient depth and multisample color are discarded after resolve. This extra storage,
resolve bandwidth, storage-buffer vertex reads, opaque fragments and queue submission
can cost more than the avoided work on some devices. The current extension preparation
contract requires one world submission before the ordinary UI submission; page-bake
submissions remain demand-driven. This is not a claim of one total device submission
per frame. No change to the stable managed/native scene C ABI is involved.

The pass remains **disabled by default on every platform**. Desktop `--world-pass`
enables it together with material pages. The existing deterministic desktop/device
measurement harness accepts `SUNTRAIL_MATERIAL_PAGES=1` and `SUNTRAIL_WORLD_PASS=1`;
reports include scene redraws, declared render-target bytes, uploads and total game
draws. Reference runs use the same final binary with `SUNTRAIL_WORLD_PASS=0`.

Primary design input: [Godot GPU optimization](https://docs.godotengine.org/en/stable/tutorials/performance/gpu_optimization.html)
explains opaque depth, painter-order transparency, mobile tile rendering and the costs
of extra viewport textures. Adopt that separation; reject sorting translucent art or
using an alpha cutoff. The existing cross-engine research above remains applicable to
WinUI scene/text reuse and resource lifetime; no shaping, font fallback, glyph cache,
DPI policy or native renderer algorithm changes. ProGPU's original
`src/ProGPU.Scene/Extensions/Mesh3DExtensionPipeline.cs` at `810b7f67` is the
in-repository ownership/attachment reference; original Suntrail material/page code at
that same revision is the shader and instancing authority. No third-party source was
copied. The same canonical WGSL and game adapter run through all three managed hosts;
there is no corresponding native C++ Suntrail host to update.

Added, **not yet executed**, tests cover attachment budget/reuse/resize, all-world
painter/depth comparisons at 1x/4x MSAA with representative Retina cases, stable scene
reuse, animation invalidation, and mode switching. ShaderResourceTests, image review,
mobile recreation, expanded transparent-overlap/cold-cache/resize fixtures and matched
Release Instruments/performance runs remain required. In particular, resolving the
game before partially covered UI overlays can alter sample correlations; final UI
image review must assess this rather than assume exact equivalence from game-only
images. This pass is not enabled or installed on the disconnected iPhone.


## Bounded asset packages (implementation batch)

`Assets.AssetBundle` is a game-neutral byte container. It owns imported data, resolves
relative references inside the package and has no filesystem/network service. Loading
is O(C + U + E) for compressed bytes, expanded bytes and entries, with explicit 16 MiB
compressed/32 MiB expanded/128-entry ceilings. ZIP central-directory preflight bounds
BCL entry allocation before decompression. Each file is read to its declared bounded
length with one extra-byte probe; its CRC is checked. A small CRC table is generated
once from the standard polynomial, not copied from a foreign implementation. Stored
and deflated single-disk archives are supported; ZIP64/encryption/multi-disk formats
are explicitly rejected. The standard BCL owns decompression.

The format authority is [PKWARE APPNOTE](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT),
sections 4.3.12/4.3.16 and the CRC contract. Only public record fields and algorithms
inform the original implementation. Suntrail's importer owns manifest semantics,
Tiled gameplay classes, room graph assembly and immutable level compilation; none of
those game-specific concepts are in the engine. Package bytes need not remain resident
once this conversion completes. No parsing, decompression, allocation or path resolution
is added to simulation or rendering. The same managed BCL/reader code is shared by
Desktop, iOS and Browser; no native renderer or stable ABI change applies.

Regression source and documentation are added; execution, host picker validation and
fuzz/property coverage remain in the final validation phase requested by the user.

## Shared source instances and retained uploads (implementation batch)

The existing device measurement recorded 65,791,680 uploaded bytes for the material
renderer versus 4,674,528 for direct sprites across 600 measured frames. Each visible
material page repeats its source sprite's bounds, tint, recipe and size. Camera
movement consequently changes a large expanded stream even when page UVs are stable.
This is a secondary cost introduced by the material cache; the recorded dominant
fragment cost still motivates the separate depth-pass experiment.

The opt-in shared-source path separates a 64-byte `MaterialSourceInstance` per
visible source from a 48-byte `MaterialPageReference` per visible page. References
hold source/atlas rectangles, a uint source index and a residency bit. Both the
painter path and the depth path reconstruct the original `PageSprite` in the vertex
shader and call the same original `page_vertex` and fragment functions. Painter
order, tree deformation, source UV arithmetic, float32 fields, exact opaque-alpha
classification, live fallback and material recipes remain unchanged. No material
precision or framebuffer resolution is reduced.

`RetainedGpuBuffer<T>` owns a fixed-size GPU buffer and CPU shadow. It compares
records by exact bytes, finds the first/last difference, and uploads at most one
enclosing range. This deliberately accepts unchanged interior records to avoid
per-record native calls. Count-only shrink invalidates the generation without a
write; growth uploads the newly exposed tail. Identical records produce no write,
allocation or generation change. Signed zero and NaN payloads are preserved.
The borrowed input span is consumed synchronously. Only the owner writes its GPU
buffer; callers may bind or read it. Disposal follows the backend's existing
submission-aware resource disposal. New device owners start with empty streams.

For S sources and P pages, a completely changed frame sends 64*S + 48*P instance
bytes instead of 96*P; the comparison remains O(S + P), with at most two buffer
writes. When source placement changes but references remain identical, only source
records need uploading. No general reduction is promised for sparse one-page
sprites: that case has a larger combined record size. Vertex pulling adds two
storage reads per vertex, with GPU cache behavior requiring actual measurement.
The opt-in buffers occupy 512 KiB at the fixed 2048-source/8192-page capacities.
CPU staging and shadows are also bounded; the original expanded GPU buffer remains
allocated for immediate comparison-mode switches. There is no new texture storage.

The public engine types contain no Suntrail game rules. Suntrail owns source-index
assignment, initialized reserved words, residency updates and page ordering. This
is retained transport for a visible scene, not persistent world-space entity IDs:
visibility churn can reindex sources and still rewrite references. World-space
scene/chunk retention and predictive material preparation remain open.

Primary architecture references are [Unity's SRP Batcher](https://docs.unity3d.com/6000.0/Documentation/Manual/SRPBatcher.html)
(persistent material data separated from per-object data) and
[Godot GPU optimization](https://docs.godotengine.org/en/stable/tutorials/performance/gpu_optimization.html)
(batching and visibility tradeoffs). Adopt separate update frequencies and bounded
contiguous transport; reject a new ECS, per-page resource binding and a shader
variant for every asset. The cross-engine startup, shaping, scene, culling, cache,
DPI and device-ownership applicability matrix above still applies. This original
implementation refactors ProGPU's own `MaterialPageInstance`,
`ProceduralPipeline.Materials.cs` and `Suntrail.wgsl` at `810b7f67`; it does not port
third-party source. The new shaders reside only in the canonical Suntrail module.

Desktop, iOS and Browser share these managed engine records and shader sources.
There is no native C++ Suntrail game renderer; the core C ABI and paired core
compositors are unchanged. These are GPU stream records, not new public native ABI
records. A future native game consumer must use the same layouts and shaders.

Enable with desktop `--shared-instances` (implies material pages), or use
`SUNTRAIL_SHARED_INSTANCES=1` in the existing desktop/iOS measurement driver. It
can be combined with `--world-pass` / `SUNTRAIL_WORLD_PASS=1`. The switches are
independent so the final runs can isolate transport cost from depth/resolve cost.
Source/page upload-byte and write-call counters explicitly include warmup; the
existing aggregate measured upload count retains its original measurement window.
Both new paths remain off in normal gameplay pending the final gates.

Regression source covers field offsets, owned shadows, partial edits, shrink/growth,
bitwise floating-point identity, unchanged allocation-free updates, all-world
painter/depth equivalence, mode toggling, cold fallback and recreated devices.
None of these new tests or shaders has been executed or built in this batch.
Final validation must include exact transport image comparisons, Browser AOT,
iPhone native startup, changing viewport/DPI and UI overlap, and matched Release
CPU/GPU/Metal Instruments measurements with upload, allocation and residency data.
Earlier material-cache speedups do not establish a speedup for these changes.

## Camera-independent placement and scene packing (implementation batch)

`Camera2D` and `SpritePlacement` now separate original world rectangles from camera
projection. Camera position is in world units; scale produces logical pixels, while
DPI remains a framebuffer concern. Per-axis factors preserve the existing mountain,
landmark and ruin parallax. Screen-space clouds, particles, shafts and sky retain
their current placement. Suntrail records both original placement and its projected
sprite, so the existing renderer and CPU visibility logic retain their reference
coordinates. Projection/inverse projection are constant-cost operations.

The opt-in scene-instance mode sends world bounds and camera factors through the
existing 64-byte shared source record. `SourceSize.xy` retains material size;
`SourceSize.zw` carries Suntrail's nonnegative camera factors, with negative Z
marking a source already in logical screen pixels. The canonical vertex shader
projects world bounds once per vertex, then follows the same page deformation and
fragment functions. One 16-byte camera field extends the shared frame uniform from
288 to 304 bytes; ordinary, sky-bake and material-bake bindings use the same updated
layout. This changes no core compositor or public native wire record.

Static sources precede dynamic sources in the storage buffer. A separate index map
preserves page painter order, including transparent and animated artwork. Moving
platforms, mechanisms, enemies, the courier, particle instances and clouds are
classified as changing placement. Shader-only animation remains independent of CPU
instance updates. Classification is only a packing hint: every source still goes
through byte comparison, so checkpoint changes or other infrequent mutations of a
static source are never ignored. Source packing is two bounded O(S) passes with no
sorting or per-frame allocation. The retained buffer can upload the dynamic suffix
without rewriting a stable static prefix. Camera-only changes no longer alter world
source bounds when the visible source set is stable.

This is not a persistent entity table: objects entering/leaving visibility and
pickups/enemies disappearing can still reindex later sources and page references.
Stable world IDs, chunk lifetime and predictive material preparation remain open.
GPU projection uses the same float32 equations, but compiler contraction can differ
from CPU projection in the last bits. Final quality gates include subpixel camera
movement and all-world images; no pixel-equivalence or FPS claim is made yet.

`VisibleCellRange` also bounds repeated ground-decoration generation before hashing
or creating records. Selection is O(1); work scales with visible cells and two guard
cells instead of the full ground segment. The footprint preserves the original
32-unit placement jitter, 76-unit fern reach and 12-unit grass overhang. Original
terrain-relative indices remain the seeds, so visibility selection does not create
new artwork or shift decoration placement. Other engine consumers can use the same
interval helper with their own conservative footprint. For much larger coordinate
scales, footprints must include numerical error or callers must rebase coordinates.

The architecture extends the persistent-material/per-object separation described
by [Unity's SRP Batcher](https://docs.unity3d.com/6000.0/Documentation/Manual/SRPBatcher.html),
and the per-axis camera factors follow the behavior documented by
[Godot's parallax guide](https://docs.godotengine.org/en/stable/tutorials/2d/2d_parallax.html).
Adopt camera-independent source data and separate changing data; retain the existing
single-camera, CPU-visible scene and reject a general scene graph/ECS rewrite for
this workload. All formulas and artwork come from the original ProGPU
`ProceduralBatch.cs` and `Suntrail.wgsl` at `810b7f67`, refactored into typed engine
values and the same canonical shader. No external implementation was used. The
cross-engine research/applicability matrix above still applies: UI shaping, glyph
caches, layout, fallback, subpixel rules and core managed/native renderers are
unchanged. All three Suntrail hosts consume the same engine implementation; no
native C++ Suntrail scene counterpart exists.

Desktop `--scene-instances` enables material pages, shared sources and camera/scene
packing. The corresponding measurement switch is `SUNTRAIL_SCENE_INSTANCES=1`;
`SUNTRAIL_WORLD_PASS=1` remains independently combinable. The final report includes
the static source count along with source/page upload counters. None of these new
modes is enabled in normal gameplay or installed on the iPhone yet.

Regression source now covers camera round trips, every recorded source's CPU
projection across all worlds, static-prefix upload bounds, scrolling/parallax and
transparent ordering in painter/depth paths, plus randomized visibility ranges,
exact edge contacts and work independent of total level length. These cases and
all related builds, shader checks, screenshots and native measurements have not
been run, following the requested deferred-validation workflow. The prior measured
26% frame-interval reduction belongs to the installed material-cache revision only.

## First rendering-architecture gate, September 5

The new renderer now builds in Desktop and signed iOS Release. Focused render
coverage passed 55 tests; shader-resource coverage passed 19. The earlier batch
notes describing these checks as unrun are historical. Gameplay/importer, full UI
overlap, Browser AOT and phone runtime validation remain open. The phone was
unavailable during this gate; no newer renderer installation is claimed.

Three interleaved runs per mode on the same Mac Release binaries gave median-run
p50/p95/p99 serialized completion of 6.816/8.703/9.131 ms for material pages and
5.741/7.790/8.601 ms for shared world-space instances plus the depth pass. CPU
submission was 0.667/1.407/1.808 versus 0.780/1.339/1.843 ms. Each run used 120
warmup and 600 measured frames, world 1, 2796×1290 at DPI 3/MSAA 4, and ended at
simulation tick 1200, x=2944.5835, y=299.2224, zero deaths. The new mechanics mean
this workload cannot be compared directly with the earlier material-engine runs.

Measured upload bytes fell from 63,664,272 to 19,570,112; final live Metal bytes
rose from 285,392,896 to 421,642,240. Draws increased from 13,804 to 15,004 because
of the opaque pass and UI resolve. GPU projection without depth gave no latency
benefit in this run set (p50 6.911 ms); shared records alone gave 6.637 ms. This
separates useful transport reductions from the depth-path latency hypothesis.

Raw CSVs, exact commands, binary hashes, dirty-tree/environment metadata and test/
build logs are under `artifacts/suntrail/performance/scene-architecture`. These
observations do not establish phone FPS or complete the Instruments/platform
performance gate; all new modes remain opt-in pending those results.

Matched five-second Metal System Trace and Time Profiler captures are now exported
for the material-page and depth modes. All four captures ended at their requested
time limit; nonempty tables, row counts and hashes are recorded in
`mac/trace-manifest.json`. Raw trace bundles were removed after verified export,
as requested. One incomplete initial capture was discarded and documented; the
retry allowed sufficient time for Instruments to finalize its output.

The 1.5–4.5 second process-filtered Metal window contains 1,714.910 ms of fragment
activity in material-page mode and 1,228.173 ms in depth mode, with more vertex and
compute activity in depth mode. Intervals are unioned to avoid nesting double
counts. These windows complete different numbers of frames and are diagnostic
only, not normalized GPU-frame timings. Time Profiler includes startup/JIT work
and unresolved managed addresses, so its sample totals do not establish a steady
CPU improvement. The uninstrumented serialized comparison remains the latency
evidence. Matched steady EventPipe/Allocations correlation and on-device sustained
pacing/memory remain open; the earlier Allocations launch hang was not retried.

## Prepared artwork inspector drawing (implementation batch)

The editor's image inspector uses the public typed drawing-extension factory, with
resources owned by each compositor rather than by a static UI control. Immutable
CPU RGBA images retain their identity across device recreation. Preparation performs
first-demand texture upload and exact changed-uniform comparison using the original
in-repository `RetainedGpuBuffer<T>` implementation. Stable replay reuses textures,
bindings, pipelines and uniforms. Hidden views release their resources through
the existing context disposal queues after the frame. The adapter caps eight view
owners, 128 images and 32 MiB pixel residency; this is a small image-inspection
renderer, not the eventual many-object level sprite batcher. One owner may occur
once per extension frame. Concurrent targets sharing a view owner are not supported.

`RasterArtwork.wgsl` is a canonical shader resource shared by the three sample
platforms. It projects a quad through the actual compositor projection and samples
four source texels, clamps them inside the selected frame, premultiplies each sample
before interpolation, and applies accumulated opacity. It avoids frame-edge bleed
and straight-alpha interpolation fringes. There is no image decode, readback or
extra queue submission. Rectangular clips and source-over are supported; mask clips
and other blend modes fail explicitly. Mipmapped/perspective minification and full
sprite placement are separate work. Complexity is O(V + F) for visible inspector
views and fragments; four loads per fragment and six vertices per view.

The existing `CurrentProjection` and `ActiveOpacity` compositor getters are now
public read-only contracts for external extensions. Their storage and calculations
are unchanged. Projection is consumed during prepare and opacity during compile,
including retained replay invalidation and offscreen projection changes. This is
an applicability-specific managed API exposure: the CLR `ICompositorExtension`
factory has no native C++ Suntrail-host equivalent. No native ABI, paired core
renderer algorithm, font/shaping contract or projection calculation changed.
The inspector shader is sample-owned; a future native sample host must consume
that same canonical resource.

Research revisited the primary links already recorded above: Direct2D/Win2D resource
reuse and deferred submission informed explicit ownership; WebRender and Vello
reinforced preparation separate from drawing; Godot's batching guidance supports
retention but a per-inspector draw is not a substitute for a world sprite batch.
Skia, DirectWrite, Parley and HarfBuzz retain text shaping/layout responsibilities
in the existing UI renderer. Startup remains lazy, culling uses existing visible UI
commands, image identity is immutable, eviction is bounded, DPI is handled through
the compositor projection/physical target, and device loss creates a new extension.
Background image preparation and workload measurements remain deferred. No foreign
implementation text was copied; the GPU setup/disposal approach follows original
`ProceduralPipeline.cs` / `.SkyCache.cs` in this repository and the retained-buffer
implementation introduced on this branch.

Regression source covers frame clamping, premultiplied opacity, RGBA/BGRA targets,
MSAA 1/4, DPI 2, changing logical target sizes, unchanged uploads, retained UI on a
new device and hidden-resource release. This implementation batch remains unbuilt
and unrun; ShaderResourceTests and GPU correctness/performance gates are outstanding.
No FPS or image-quality improvement is claimed from the new code alone.

## Retained artwork sprite batches (implementation batch)

The preceding inspector-only contract is extended to a shared typed
`RasterArtworkBatch` command. Single-image inspection records the same batch
contract with one sprite. An immutable snapshot owns up to 65,536 ordered sprite
records and derives its actual union bounds; all source frames and destination
bounds are validated before recording. The public extension remains sample-owned.
The batch payload replaces the previous single-view payload on this unmerged
branch. Existing `DrawArtwork` callers continue through a convenience wrapper.

Compilation packs 32-byte destination/source records, groups only adjacent image
identities into runs, and collects unique images. It never sorts translucent
sprites across another image. Preparing a batch pins all its resident images
before demand uploads so a new image cannot evict an image needed later in that
same batch. Eight active owners share the 128-image/32-MiB texture budget; a batch
that exceeds those bounds fails explicitly. Per-owner instance capacity grows to
a power of two, capped at 65,536; shrinking changes the active count and retains
capacity. This adds at most 16 MiB of GPU instance storage across eight owners,
with matching CPU shadows plus O(N) immutable snapshots and compiled records.

The original in-repository `RetainedGpuBuffer<T>` updates one enclosing changed
range. Stable compiled replay skips instance comparison and upload; frame uniforms
retain byte comparison for projection and accumulated opacity. A batch owns an
80-byte uniform buffer and one storage buffer, rather than buffers per sprite.
The pipeline selected for each prepared owner is retained with that owner, so
preparing another owner's target format/sample count cannot change its pipeline.
Each contiguous run binds its image and draws six vertices with its instance
count and first-instance offset. There is no extra queue submission, image decode
or readback. The old one-owner-once-per-extension-frame restriction still applies.

`RasterArtwork.wgsl` remains the single canonical shader for Desktop/iOS/Browser.
Source-frame bounds now travel as flat instance-derived varyings; four-texel
premultiplied bilinear sampling, frame-edge clamping, rectangular scissoring and
physical-target projection are unchanged. Compilation is O(N), preparation O(A)
for unique images A plus changed instance data, and rendering O(R + N + F) for
contiguous runs R, sprites N and fragments F. Alternating images can still require
one draw per sprite. Texture-atlas packing and broad sorting are not claimed by
this implementation; board placement and semantic z-order remain separate work.

Research and applicability were revisited before changing the batch architecture:

- [Direct2D performance guidance](https://learn.microsoft.com/en-us/windows/win32/direct2d/improving-direct2d-performance) and [Win2D device hosting](https://learn.microsoft.com/en-us/windows/apps/develop/win2d/using-win2d-without-built-in-controls) inform device-owned resource reuse and avoiding explicit per-sprite flushes. Atlas packing is a later measurable choice; retained sheets already provide bounded source regions.
- [WebRender](https://github.com/servo/webrender) and [Vello](https://github.com/linebender/vello) remain architectural references for separating retained content from GPU preparation. This adapter uses instance storage for raster quads, without adopting another renderer's implementation or general vector compute pipeline.
- [Skia text architecture](https://skia.org/docs/dev/design/text_overview/), [DirectWrite](https://learn.microsoft.com/en-us/windows/win32/directwrite/programming-guide), [Parley](https://github.com/linebender/parley), and [HarfBuzz shape plans](https://harfbuzz.github.io/shaping-and-shape-plans.html) retain their role in the earlier text comparison: shaping/layout reuse, font fallback and variable-font state belong to the existing CPU/UI text path. No text cache, DPI snapping, glyph upload or font behavior changes here.

Startup stays lazy; UI visibility controls command preparation; immutable image
identity supplies cache keys; hidden owners/images use existing deferred disposal;
new devices create independent pipeline instances. Decoding remains preparation
work outside drawing. Worker scheduling and atlas packing are not introduced in
this batch. No third-party implementation was copied: provenance is the original
`RasterArtworkDrawing.cs` and `RasterArtwork.wgsl` inspector implementation on this
branch, plus `ProGPU.GameEngine/Rendering/RetainedGpuBuffer.cs`.

This is the managed shared Suntrail sample extension; no native C++ Suntrail host
or corresponding native sprite algorithm exists. The core managed/native renderer,
public C wire contracts and generated declarations are unchanged. A future native
host must consume this same canonical shader and match these output/retention
contracts. Existing inspector regressions now exercise the batch shader; added
source covers immutable ownership, bounds, more than eight sprites, painter order,
adjacent-image draw counts, growth/shrink, unchanged uploads and empty batches.
All builds, shader-resource audits, GPU tests and matched Release/Instruments
measurements are deferred to the final gate. This code alone proves no FPS gain.

## Single-quad tiled nine-slice artwork (implementation batch)

`RasterArtworkSprite.NineSlice` optionally carries integral source-border X/Y and
logical pixels per source pixel X/Y. Zero retains ordinary sprite sampling. The
batch validates nonempty center strips, finite positive scales and destinations
large enough for both borders, with local extents capped at 65,536 source pixels.
The board supplies the camera zoom as the scale for a sizable block, keeping
pattern size consistent with ordinary sprites as the editor zooms.

The canonical `RasterArtwork.wgsl` remaps each axis into its leading border,
periodic middle, or trailing border. Bilinear taps clamp inside corner patches and
wrap inside repeated strips using positive modulo. Each tap premultiplies before
interpolation. This preserves the existing four-load fragment footprint and one
six-vertex quad per block regardless of repeated tile count. Added branches,
coordinate arithmetic and interpolants still require matched performance evidence;
no speed or quality improvement is asserted without the deferred tests/profiling.
Instance stride grows from 32 to 48 bytes, increasing the maximum eight-owner GPU
instance capacity from 16 to 24 MiB, plus corresponding CPU shadows. The texture
budget, batching order, deferred disposal and submission count are unchanged.

This continues the earlier cross-engine design comparison. The revisited
[Direct2D resource guidance](https://learn.microsoft.com/en-us/windows/win32/direct2d/improving-direct2d-performance)
supports retained bitmap reuse; the selected approach keeps one original sheet
instead of allocating expanded bitmaps or geometry per repeated tile. WebRender,
Vello and Win2D preparation/device-ownership concepts from the preceding research
remain applicable. Skia/SkParagraph, DirectWrite, Parley and HarfBuzz shaping,
fallback, font-cache and DPI/subpixel contracts remain in the existing UI text
path. No new worker scheduler, text engine or atlas packing scheme is introduced.

Provenance is the original in-repository `RasterArtworkDrawing.cs`,
`Shaders/RasterArtwork.wgsl`, `SmbxBoardArtwork.cs` and retained-buffer implementation
on this branch. Managed Desktop/iOS/Browser share the shader and packed layout.
No corresponding native C++ Suntrail sprite host exists; core managed/native scene
algorithms, C ABI and generated wire data remain unchanged. The shader is mandatory
shared source for any future native implementation of this sample algorithm.

Independent test source builds expected pixel rows/columns by explicitly repeating
source strips and compares full GPU output for RGBA/BGRA and MSAA 1/4, minimum
border-only destinations and partial tiles. CPU regressions cover bounds, malformed
sizing and one-placement retention. These are written but not run; shader-resource,
visual, device, allocations and matched Release/Instruments gates remain deferred.

## Shared static collision and character motion (coding batch)

Added `StaticCollisionWorld2D` and `KinematicCharacter2D` under the reusable engine's
simulation namespace for upcoming imported-section runtime integration. They do
not reinterpret imported IDs as Suntrail objects and are not yet wired to a
playable SMBX runtime. The original `GameSession.MoveAndCollide` provides the
in-repository axis-separated movement context; this new implementation uses swept
nearest boundaries instead of final-overlap resolution and keeps coordinates in
double precision. Existing Suntrail behavior is unchanged by this batch.

An immutable generation owns up to 65,536 collider records with distinct stable
IDs, sorted by their left edge and indexed by prefix maximum right edges. Two
binary searches bound a query, followed by candidate scanning and vertical
rejection: O(log N + K), worst O(N) with many overlapping long intervals. Build is
O(N log N) time/O(N) storage. Axis sweeps choose the nearest solid boundary and use
stable IDs for coincident contacts. One-way tops are active only while descending
from at/above their surface; explicit drop-through skips them. Initial penetration
is reported instead of silently resolving it in a record-order-dependent direction.
This is an axis-separated kinematic model, not a general continuous rigid-body or
diagonal time-of-impact solver. Slopes, dynamic platforms and crush behavior remain
separate runtime rules.

The character component consumes an explicit immutable motion policy and fixed
step: speed/acceleration, gravity/fall cap, jump/release behavior and bounded input
grace ticks. It holds previous/current double-precision rectangles, velocities,
facing and contact state. Teleport validates the destination before replacing the
world/body, optionally retaining momentum. Step uses span-based queries without
allocating or reading source files. No foreign game's coefficients or character
defaults are supplied by this component. Host input accumulation, section limits,
camera behavior, object rules, art and runtime lifecycle still need integration.

No renderer, shader, C ABI or native wire layout changes occur; all three managed
sample platforms can use the shared CPU code. No equivalent native C++ game
simulation exists in the repository. This is original ProGPU implementation, with
no foreign source text or data copied. Per the accelerated sequence, builds,
conformance/differential tests, input/playability checks and performance measurements
are deferred to QA. No tunneling/FPS improvement is claimed for the running game
until integration and those checks are complete.

## Source geometry playtest integration and scrolling retention

`SmbxPlaytest` connects `StaticCollisionWorld2D` and `KinematicCharacter2D` to an
immutable source document and internal editor projection. Preparation owns sorted
block colliders, at most 256 section bounds, 4,096 source warp records (up to two
connections each), and 128 displayed issues with a total counter. It does not
translate unknown IDs into Suntrail NPCs or claim original SMBX physics. Source
blocks deliberately become solid rectangles; the 30 × 48 courier uses original
Suntrail coefficients from `GameSession`, with fixed 120-Hz stepping, at most 12
catch-up ticks, queued jump/use edges and pause/deactivation reset. Source geometry
remains double precision until the camera-relative drawing boundary. No new native
crossing or native engine ABI is involved.

Warp data follows the previously consulted [LVLX field specification](https://github.com/WohlSoft/PGE-File-Library-STL/blob/master/docs/PGE-X/LVLX%20file%20description.pdf):
DT 0 resets momentum, DT 3 preserves it, directions use the distinct entrance/exit
contracts, and STR requires ground contact. The playtest deliberately supplies a
32-unit aperture thickness/default length, immediate USE-triggered pipe/door
transfer and a one-second lockout. It rejects external/map/one-ended transitions,
requirements/events/cannon rules and obstructed or out-of-section destinations.
Both directions of a two-way record prepare atomically. These explicit playtest
choices are not claims of SMBX engine equivalence. NPC behavior, layer/events,
slopes, block effects, camera effects and original player profiles remain open.

`SmbxPlaytestView` runs above the retained editor without replacing source drafts.
It reuses the original `GameView.HoldButton` capture/cancellation implementation,
now `TouchHoldButton`, and existing `TouchStick`. Control choices share the main
settings persistence seam. Main-game keyboard alias tracking preserves other held
movement/jump/sprint keys on release. `ProceduralBatch.BuildCourierPreview` reuses
only the original courier/shadow construction from `ProceduralBatch.Build`; the
canonical shader is unchanged in this batch. Desktop, iOS and Browser all consume
these shared C# sample files. A native C++ Suntrail/imported-game host does not exist,
so there is no duplicate native gameplay implementation to update.

The source board now retains painter-ordered sprite snapshots with 192 logical
pixels of overscan, reusing them for camera translations within 96 pixels per axis.
Sprite preparation is O(N + V); camera-only sprite recording within the retained
region is O(1), followed by O(R) render runs. Editing overlays still scan O(N).
Art/source/drag/layer/zoom/viewport changes invalidate the snapshot. Rebasing occurs
in double precision before packed float geometry. The actual command transform
carries translation, keeping clipping, projection and physical DPI authoritative.

Each immutable `RasterArtworkBatch` lazily retains one CPU-packed geometry snapshot
(48 bytes per sprite after nine-slice support), image runs and image identities.
Identical-opacity compilation reuses its immutable command payload. Changed opacity
creates a small payload sharing geometry; preparation skips instance comparison and
upload when batch identity is unchanged. First concurrent compilation can produce
bounded duplicate candidates before atomic publication; no GPU resources cross
compositor device domains. Context-owned buffers/textures retain their existing
residency and disposal rules. This adopts retained display-list/resource reuse from
the primary Skia, Direct2D/Win2D, WebRender and Vello research recorded above; no
foreign implementation is copied. Text remains in the unchanged shaped UI path
(DirectWrite/Parley/HarfBuzz applicability unchanged). The native C++ renderer does
not consume this sample-specific extension payload; public compositor getter
exposure, common rendering code and shader equivalence still need the final audit.

Final QA must cover source/undo preservation during play, negative/large-coordinate
collision, fixed-step input parity, all warp directions/blocked endpoints, device
suspend/resume, simultaneous touch/key release, translated sprite pixel equality,
overscan edge crossings, empty and changed batches, opacity/zoom/DPI/layer changes,
multiple contexts and Release upload/allocation/frame-time evidence. No tests,
builds, visual checks or performance measurements ran for this coding batch.

## Authored campaign diversity and enemy profiles

`CampaignRoute.Design.cs` prepares original encounter terrain and rewards alongside
explicit overworld/vault scores. Preparation is bounded O(S + P + E + C), where S is
the small authored section count and P/E/C are generated platforms/enemies/pickups.
No route file parsing, asset discovery, procedural random search or new per-frame
allocation was added. Runtime uses the existing platform/contact mechanism rules.
The generic repeated upper-gallery generator was removed, rather than layered
under the new encounters. Each world retains three relic encounters and persistent
vault coin state. Native pipe transfers now match two early/late overworld endpoints
with two vault endpoints; completing the shortcut advances a safe respawn point.

Enemy policy adds a 2.1-second hopping cycle (a 1.155-second bounded parabolic arc,
96-unit peak, then rest) and sinusoidal hovering 66–134 units above its authored
anchor. They are original game policies, not third-party character algorithms.
Both retain existing horizontal patrol bounds and 42 × 34 collision bodies. Stomp
classification compares the previous enemy top against the previous player feet,
so vertical enemy movement does not classify from mismatched frame positions.
Editor and Suntrail/Tiled serialization append explicit hopper/hoverer classes;
existing enum IDs remain unchanged. The route pilot remains input-only and predicts
moving/timed support landings without mutating platform state.

Canonical `Suntrail.wgsl` extends the original beetle artwork with two bounded wing
ellipsoids and a vein for hovering insects, or a horn for hoppers, plus authored
shell colors. Original sphere/stroke/premultiplied-over helpers are reused directly.
There are no new loops, texture samples, wire records or GPU resource types.
Beetles remain dynamic fallback sprites, never baked as static material pages;
shadows remain attached to their ground-relative anchors. Narrow terrain piers
omit oversized tree/bush decorations. Existing DPI, coverage and target contracts
remain applicable and require final pixel/performance regression checks.

All sample hosts consume these same C# gameplay sources and canonical shader;
there is no native C++ Suntrail game implementation to duplicate. No foreign
implementation text, game maps, commercial artwork or lookup tables were used.
No QA or performance claims are made for this batch. The final pass must include
every route/relic/vault, both pipe directions and checkpoint persistence, editor
save/reload, enemy motion/stomps, canonical shader gates, and matched Release
image/allocation/upload/frame-time measurements on the final binaries.

## Shared spatial hierarchy and explicit geometry generations

`StaticSpatialIndex2D` now supplies a small reusable CPU visibility/picking primitive.
It accepts up to 131,072 positive finite rectangles with distinct nonnegative IDs.
Preparation interleaves 16 quantized centroid bits per axis, sorts by that key/ID,
and builds a balanced median tree. Bounds retain original doubles; key quantization
only affects locality, never coverage. Parent unions expand a rounded-in right or
bottom extent outward. Empty and finite-union constraints are explicit. This is an
original CPU implementation; it does not implement another engine's radix builder.

The primary [Karras 2012 research abstract](https://research.nvidia.com/publication/2012-06_maximizing-parallelism-construction-bvhs-octrees-and-k-d-trees)
was consulted for spatial ordering/hierarchy construction concepts. Its parallel
GPU radix construction is rejected for this small, immutable preparation workload;
no paper/source implementation was copied. The existing primary Skia,
Direct2D/Win2D, WebRender, Vello, Parley and HarfBuzz research above continues to
inform scene/resource reuse and separation from unchanged UI text. This change
adds no text/font cache, GPU resource, native crossing, wire layout or shader.

Build work is O(N log N) with O(N) owned storage and bounded temporary ordering data.
Query work is O(Q + V log V), where Q is the number of visited hierarchy nodes and
V the returned items; worst case remains O(N + V log V). A balanced tree at the
maximum size has 18 levels; depth-first traversal uses 64 stack integers. The
caller's output must fit N IDs, and only the returned prefix is valid. Sorting IDs
preserves painter order independently of spatial order. Queries allocate no arrays
or per-item delegates. Detailed allocation/latency evidence remains a final QA gate.

`Level` publishes an immutable platform array and conservative visibility hierarchy.
Native platform bounds cover full horizontal/vertical sine travel, except conveyor
travel which is belt speed, plus existing crown/foliage/edge/shake extents. Both
procedural platform recording and the conservative ground occluder pass query the
same generation. Occluders now use current platform positions. A between-frame
`ReplacePlatform` transaction validates a non-pipe replacement, prepares the next
array/tree, resets its timed support and updates camera width before incrementing
`GeometryGeneration`. GameSurface observes level identity and that generation even
while paused. Pipe geometry changes remain complete document transactions so pipe
indices cannot silently diverge from solid geometry. Existing test callers were
migrated; this contract has not yet been tested.

The SMBX board uses the same hierarchy for sprites, overlays and picking. Bounds
include source rectangles/anchors and prepared graphic offsets/extents. Actual
paint/hit predicates remain authoritative after candidate selection. Source/art
replacement prepares one matching hierarchy before drawing. Layer visibility
filters candidates without rewriting source geometry. A drag keeps the immutable
source generation and inserts its selected ID into the ordered candidate prefix,
so moving out of the original leaf remains visible/selectable; no per-pointer tree
rebuild occurs. Commit/undo rebuild from the new immutable document. Selection-only
updates preserve retained sprite data. Source-coordinate rebasing, overscan and
DPI/projection rules from the previous batch remain in place.

This is shared C# game/sample functionality consumed by Desktop, iOS and Browser.
There is no native C++ Suntrail scene model consuming these records; core native
rendering and canonical shaders are unchanged. Final QA must cover ordered equality
with exhaustive queries, touching/empty/large-coordinate/degenerate-union cases,
worst-size and overlapping scenes, active drags and hidden layers, paused geometry
changes, moving platforms, opacity/zoom/DPI/resize and device recreation, followed
by matched Release query/recording/allocation/upload and frame-time measurements.
No build, tests, visual QA, profiling, commit or push ran for this coding batch.
