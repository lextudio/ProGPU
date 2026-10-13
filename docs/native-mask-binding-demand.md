# Native mask binding demand

The final Gallery package CPU recording at LibreWPF `b620e8e1` samples the
exact ProGPU `4bbd1dc0` ARM64 DLL. Of 44,032 owned-process CPU samples, 26,095
have FXC as the leaf; 11,564 lack stacks and no ETW events were lost. Exact
DLL disassembly associates 2,371 and 2,510 samples with the ordinary and masked
group composite pipelines. The original application still exhausts its
60-second deadline before activation. These samples attribute work; they do
not establish a complete wall-time profile or qualify Gallery.

Mask bindings previously required both group pipelines, and group resources
required the ordinary image pipeline. A masked vector/text draw could therefore
compile three unrelated image/layer pipelines just to acquire layouts, samplers
and mask uniforms. Native mask producers now request binding resources directly.
Actual image and layer draws retain their original pipeline acquisition paths.
Separate readiness flags publish only after complete binding construction;
partially constructed bindings remain rejected and owned until engine teardown.

The authoritative implementation is ProGPU's original image/layer resource
creation at `311e8688bf7b5dd31146e5b77d6e747b3d54f4cc`, whose tree equals the
qualified `4bbd1dc0` source. Both native providers compile these shared C++
sources. Shader bytes, entrypoints, vertex formats, blends, target formats,
sampler settings, draw ordering, submissions, source ownership and caches are
unchanged. There is no compiler/adapter switch, pixel tolerance, altered fixture
or application deadline. Construction remains O(1); the two engine-owned
readiness flags do not introduce a new shared lifetime or eviction policy.

The managed applicability audit covers `Compositor.cs`, `CompositorOptions.cs`
and `Compositor.HintedGlyphPaint.cs`. Managed bind-group layouts are already
created independently of drawing pipelines; ordinary draws acquire pipelines
through the existing cache. Explicit `PrecompileBasePipelines` remains a caller
policy. This change corrects native prerequisite coupling rather than changing
that policy or adding a second managed rendering path. Existing managed lazy
pipeline/cache tests and full renderer gates remain required.

## Independent validation

The additive WebScene test forwards real provider pipeline creation without
altering descriptors. It reuses original analytic-mask, masked media and masked
layer fixtures in mask-first and layer-first orders, with two independent engine
owners. It requires zero image/layer pipeline creation for mask-only demand,
exact later image/layer creation counts, no recompilation, unchanged retained
warm uploads and exact cold/warm complete BGRA frames. Its baseline mode retains
the original eager counts; all 20 original frames are available for complete
baseline/candidate comparison. The original full corpus and its 60-second
limit are unchanged. Whole producer CI, matched final-binary profiling and
original downstream Gallery acceptance remain required before publication.

Initial local Release evidence: both native providers compile, and the demand
test passes. Baseline/candidate/candidate/baseline processes explicitly verify
their actual loaded libraries and produce identical bytes for all 20 frames
(SHA256 `72e613c961ed77e919eb1d81df995dd335e29680a6a1bfd97851eb56817d8a03`).
The baseline is the exact whole-qualified producer's native runtime artifact,
not a reconstruction. Complete renderer/corpus, profiling and Windows package
qualification are still pending; these bounded observations are not Gallery
acceptance or a statistical performance claim.

## Research and limits

Primary references were revisited before implementation on 13 October 2026:
[SkParagraph layout and paint contracts](https://github.com/google/skia/blob/main/modules/skparagraph/include/Paragraph.h),
[Direct2D resource reuse](https://learn.microsoft.com/en-us/windows/win32/direct2d/improving-direct2d-performance),
[DirectWrite reusable layout](https://learn.microsoft.com/en-us/windows/win32/directwrite/text-formatting-and-layout),
[Win2D resource and device lifetime](https://microsoft.github.io/Win2D/WinUI3/html/LoadingResourcesOutsideCreateResources.htm),
[WebRender preparation and retained rendering](https://firefox-source-docs.mozilla.org/gfx/RenderingOverview.html),
[Vello GPU rendering](https://github.com/linebender/vello),
[Parley text layout](https://github.com/linebender/parley), and
[HarfBuzz shaping responsibilities](https://harfbuzz.github.io/what-does-harfbuzz-do.html).
Only the existing lazy resource ownership principle is adopted. No external
implementation text is included. Shaping/layout reuse, retained scenes, culling,
glyph/texture/path cache keys and eviction, uploads, workers, GPU batching,
DPI/hinting, font fallback/variation and device/atlas invalidation remain as
recorded in the existing pipeline research comparison. No macOS performance,
memory or application startup improvement is claimed from the Windows sample
counts; matched final-binary Instruments and complete application checks remain
separate gates.
