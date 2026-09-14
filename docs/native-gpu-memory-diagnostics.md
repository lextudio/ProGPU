# Native GPU memory diagnostics

## Application blocker and scope

Acceptance application: **ProGPU.Wpf.ShowcaseApp**. Action: complete live input
validation and measure 120 warmed presented frames. Its native performance report
must consume native ownership, not an idle managed compositor's allocation data.
This checkpoint supplies the ProGPU C++ inventory and generated managed API;
the WPF report connection and final application qualification remain separate.

`NativeCompositor.GetGpuMemorySnapshot()` returns one generated 96-byte record
through `progpu_native_engine_get_gpu_memory_snapshot`. The C ABI remains version
4: this is an additive function/record, not a resized existing contract. Both
native providers export it. Call on the owning thread between engine operations.
Invalid size/thread/device state fails without publishing a partial record.

## Ownership and byte meaning

The inventory visits buffers and textures directly retained by the engine:
ordinary frame resources, glyph/path/color atlases, analytic/image/3D pages,
layers/effects/clips, shared pictures, render-bundle masks, hit-query resources
and submission-retained raster batches. Handle identity deduplication prevents
shared picture and mask aliases from counting twice. Three layer binding-cache
buffer keys are non-owning and can be stale: they must never be dereferenced.

Buffer bytes come from the live WebGPU size getter. Texture bytes use actual
format, mip count, dimensions, samples and array/volume depth. Array layers stay
constant across mips; volume depth shrinks. Supported byte widths cover the
engine's R8, RGBA/BGRA8 and RGBA32Uint allocations. Depth24Plus and unknown future
formats contribute texture counts and `UnquantifiedTextureCount`, not guessed
bytes. `HasCompleteTextureByteCount` describes only this logical accounting.
`TotalKnownOwnedBytes` is a checked sum, not total physical GPU usage.

Excluded: external render targets, externally owned images/masks, swapchains,
pipelines, driver allocation padding, and resources the engine has released but
the backend still retains. `BorrowedViewCount` counts unique borrowed view
handles, not unique underlying textures or their storage. It cannot be added to
owned bytes. `InventoryStorageBytes` is native CPU scratch capacity, not GPU
storage. No per-resource address is exposed through the public API.

The record includes scene/generation, current submission index and a monotonic
engine identity scoped to the loaded native provider. A reconstructed engine has
a new identity; consumers must not compare growth across reconstruction or sum
engine inventories as a whole-device allocation counter. Two engines can share
resources. Pending submission batches remain counted until normal retirement;
inspection itself does not poll, wait, retire, purge, upload or read back.

## Cost, provenance and paired implementation

Original ProGPU ownership in `progpu_native_engine.hpp`, semantic replay storage
and `submission_resources` is the source of truth. The existing batched native
metrics and generated contract patterns provide transport. Internal headers
extend that existing provider-selected engine graph; no public C++ module
surface or foreign implementation is introduced.

For R retained handle references, collection is O(R), identity sort/dedup is
O(R log R), descriptor queries are O(U + M) for U unique resources and M total
mip levels, and retained scratch is O(R). Scratch keeps its high-water capacity.
This is explicit diagnostic work, not an unconditional per-frame traversal.
Opaque getter calls, pointer sorting and dependent mip arithmetic are not a
data-parallel CPU fallback; no GPU kernel, CPU raster work or SIMD policy changes.

Managed/native applicability: both renderers require honest owner-scoped metrics,
but their GPU resource ownership graphs differ. This API enumerates C++ handles
that managed ownership cannot inspect. Managed rendering/allocation behavior is
unchanged; the native path must not reuse its counters. Rendering, shaping,
visibility, upload, eviction, batching and shader defaults are unchanged.

Primary research informed the contract, not implementation text:

- [Skia context memory diagnostics](https://api.skia.org/classGrDirectContext.html):
  distinguish cache ownership, budgets and external resources.
- [Direct2D texture memory budget](https://learn.microsoft.com/en-us/windows/win32/api/d2d1_1/nf-d2d1_1-id2d1device-getmaximumtexturememory)
  and [Win2D device](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_CanvasDevice.htm):
  reject treating a configured limit as measured usage.
- [WebRender ownership overview](https://firefox-source-docs.mozilla.org/gfx/RenderingOverview.html)
  and [Vello renderer](https://docs.rs/vello/latest/vello/struct.Renderer.html):
  retain device/renderer ownership boundaries; do not sum unrelated caches.
- [SkParagraph](https://github.com/google/skia/blob/main/modules/skparagraph/include/Paragraph.h),
  [DirectWrite layout](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextlayout-hittestpoint),
  [Parley layout](https://docs.rs/parley/latest/parley/struct.Layout.html) and
  [HarfBuzz plans](https://harfbuzz.github.io/harfbuzz-hb-shape-plan.html):
  reusable CPU text state is separate from this GPU inventory. Startup, font
  fallback, variable-font state, workers, DPI and shaping reuse are unchanged.
- [wgpu texture format contract](https://docs.rs/wgpu/latest/wgpu/enum.TextureFormat.html):
  opaque depth formats cannot be assigned a portable physical byte width.

## Evidence and outstanding gates

Local macOS ARM64 Release: both providers compile; all 19 CTests pass. Tests cover
handle aliases, nulls, arrays/volume/mips/samples, opaque formats, overflow,
scratch stability and submission visitor lifetime. Real WebGPU tests verify
bad-size/wrong-thread output preservation and repeated inventories after scene
rendering. A source guard covers 91 declared owned buffer/texture fields across
nine ownership structs; it supplements tests and requires review for new
container types, rather than proving all possible future ownership.

The focused managed interop suite passes 125 tests, including generated size and
offsets. The full project-reference native package consumer passes on Metal with
ordered queries. Its real query snapshot reports 28 buffers / 330,336 bytes and
7 textures / 2,109,445 known bytes. The standalone path probe reports pending
buffer bytes 168,768, retired buffer bytes 164,320, and unchanged texture bytes
1,048,580; its pixel assertions still pass. These are fixture observations, not
portable expected allocation sizes or a performance improvement claim.

Remaining: actual Dawn-provider/Windows/Linux/browser runtime coverage of this
new API, exact package graph validation, source-host report integration, warmed
application memory-growth checks, and matched final Release Instruments/counter
measurements. The Windows default hit-query failure remains independent. No
SDK, performance, policy-default or PR merge gate is waived by this checkpoint.

Hosted follow-up: the first GCC/Linux/macOS builds compiled and reached export
verification, which rejected the new entry's position in the sorted allowlist.
Both allowlists now place the additive symbol in lexical order. Actual built
wgpu-native and Dawn export verification passes locally; no symbol or check is
removed. The new hosted run still has to qualify the corrected head.
