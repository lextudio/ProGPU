# On-demand atlas pipeline initialization

## Application dependency

LibreWPF `ProGPU.Wpf.ShowcaseApp` retains a managed composition target for shared
host services while its selected C++ MIL compositor owns rendering and input.
Creating that target previously compiled managed glyph and path raster pipelines
which a native-only frame never used. On Windows this is avoidable cold shader
work before the host can present or service its recovery dispatcher.

`GlyphAtlas` and `PathAtlas` now retain their existing binding layouts, buffers,
textures, selected execution policy and cache owner at construction, but create
the shader/pipeline only at the first actual raster request. Empty path batches,
CPU glyph modes and unused atlases do not compile a GPU raster pipeline. Glyph
immediate, batched, oversized and fragment-stage paths each ensure their selected
pipeline before beginning its pass. Later raster calls reuse the cached handle.
There is no warm-up submission, changed shader, renderer switch or deadline change.

## Paired implementation audit

Source provenance is ProGPU's existing `ProGPU.Text/GlyphAtlas.cs` and
`ProGPU.Vector/PathAtlas.cs`: the same descriptors, shader resources, entry points
and cache keys moved from constructors into demand-driven helpers. This is not a
new raster algorithm. Binding leases and pipeline-cache disposal remain unchanged;
the atlas continues to capture its typed execution policy at construction.

Both C++ providers already create glyph resources from
`Backend/progpu_native_glyph_execution.cpp` during actual glyph execution and
select requested path pipeline families during path/clip execution. They do not
instantiate these managed atlases. No additional C++ algorithm or ABI change is
needed for this managed-constructor correction. The shared host still selects the
native renderer explicitly; this change does not introduce managed rendering/input
fallback for an unsupported native contract.

## Qualification

`AtlasPipelinesCompileOnlyForActualRasterRequests` verifies zero initial shader and
pipeline counts, nonempty first glyph/path pixels, immediate and batched glyphs,
forced compute/fragment modes, unchanged captured policy after context preference
mutation, cache reuse and complete disposal. The existing explicit base-render
pipeline sharing test now expects only its three render shader modules and no
unused atlas compute pipelines before rasterization; its eight render pipelines
and layout ownership checks remain unchanged.

Windows source-host timing and final package qualification remain required. The
independent Windows native owner-query access violation is not fixed by deferring
unused atlas construction. Broader Direct2D/COM/Win2D expansion remains deferred.

Local Release validation passes all 78 selected glyph/path-atlas and immutable
compositor-resource regressions, including the four new immediate/batched GPU
mode cases. This is Metal evidence, not Windows timing or final CI qualification.
