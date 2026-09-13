# Native path atlas configuration isolation

## Blocking application path

LibreWPF `ProGPU.Wpf.ShowcaseApp` needs its first native MIL frame before
interaction can qualify. Build 34775916222 still fails the Windows x64 cold
native path and cubic frame although the canonical raw vector/coverage probe
passes. ARM64 passes those frames but fails later native owner querying.
These are separate unresolved blockers; no package pin or merge admission changes.

The raw probe's prepared vertex/index/brush payload and native payload both hash
to `37CF2B2338D40B07`. Two remaining resource differences are isolated here:

| Diagnostic | Atlas usage | Bound texture view |
| --- | --- | --- |
| `--path-native-submit-probe` | TextureBinding, CopyDst, CopySrc | Explicit managed descriptor |
| `--path-native-atlas-usage-probe` | TextureBinding, CopyDst | Explicit managed descriptor |
| `--path-native-atlas-view-probe` | TextureBinding, CopyDst, CopySrc | Null/default descriptor |
| `--path-native-atlas-probe` | TextureBinding, CopyDst | Null/default descriptor |

The reference is ProGPU's own
`src/ProGPU.Native/src/Backend/progpu_native_path_text_resources.cpp` atlas
creation, compared with `src/ProGPU.Backend/GpuTexture.cs`. This is diagnostic
code, not a rendering architecture change, new fallback, or third-party port.
Both product implementations remain unchanged. Each variant keeps the original
canonical compute/fragment shaders, bindings, geometry, sample grid, indexed
submission, retained resource lifetime and exact target pixel checks. No texture
clear, warm-up, retry or pre-draw readback is added.

The native-usage texture cannot legally be copied out; those two variants omit
only the atlas-storage readback. They still require the exact white interior and
black exterior of the rendered target, then check the original coverage buffer.
The default-view handle is retained through completion and released after its
bind group. Each workflow probe runs in an independent cold process.

## Validation and interpretation

Release project-reference build: zero warnings/errors. All three new variants
pass on Apple M3 Pro/Metal with the same payload hash and exact target samples
`(255,255,255,255)` / `(0,0,0,255)`; raw coverage is 255/0.

The manual `Native path diagnostics` workflow accepts `probe_set=atlas` and a
completed Build run ID. It preserves package version/head/hash provenance and
runs the baseline, three variants, direct native path and original cubic probe.
Historical-package diagnostics never qualify the current-head Build gate.
Windows results are still required; this change does not assert that atlas
configuration causes or fixes the black frame.

Separately, the staged Windows LibreWPF host with lazy managed atlases presents
and recovers within its unchanged deadlines, but its complete run terminates
with `0xC0000005` in `BeginHitTest` from `TryQueryHitTestBoundsOwners`. Startup
progress is not full source-host or application qualification.

## Hosted result and compute-buffer follow-up

Run `34778761952`, x64 job `103781776094`, completes with all three atlas
variants and the baseline passing. The original direct native path and cubic
frame still fail with black interiors. These isolated texture-usage/view changes
therefore do not reproduce the native failure on that runner; changing product
atlas descriptors is not justified by this evidence. The ARM64 job remains live
at this checkpoint.

Two further differences in the same original ProGPU sources are now independently
selectable with `probe_set=raster`:

- `--path-native-raster-binding-probe` declares one 48-byte segment record as
  the minimum layout binding size, as native does, while still binding all four
  records. The baseline declares the full 192 bytes as its minimum.
- `--path-native-raster-zero-probe` leaves the unused combine buffer unwritten,
  matching native ordinary-path creation. WebGPU zero initialization remains
  required; this is not admission of uninitialized shader values.
- `--path-native-raster-probe` combines those differences.

All three preserve complete raw-coverage, atlas-storage and target-pixel checks,
and pass on Metal after a zero-warning/error Release build. No product renderer
or shader is changed. Windows evidence remains required before choosing a repair.

The completed raster run `34779073956` passes all raw variants on both Windows
architectures. ARM64 also passes its two actual native frames in this run; x64
still fails both. Atlas run `34778761952` previously failed ARM64 direct native
path while its independent cubic passed, so native success is not yet reliable.
The smaller layout minimum and unwritten combine buffer do not reproduce the
x64 failure and are not demonstrated product fixes.

## Finished-encoder lifetime comparison

The native path finishes a command buffer, releases its encoder, submits, then
releases the command buffer. Earlier raw probes retained the encoder even when
testing command-buffer release. `probe_set=encoder` now compares the retained
baseline with `--path-native-release-encoder-probe` (release only the finished
encoder before submit) and `--path-native-release-encoding-probe` (also release
the command buffer after submit). Both retain the complete raster resource set
through completion and preserve all original exact coverage/atlas/target checks.
The caller clears its encoder pointer to prevent duplicate release; no encoding
occurs after finish. Product lifetime and submission code are unchanged.

Both new Release probes pass on Metal; compilation has zero warnings/errors.
Windows results remain required. Meanwhile renderer Build `34777843000` x64
consumer job `103783563847` is red: the direct native path is black, and the
original cubic readback reports DeviceLost rather than a valid image. Raw vector
and indexed-submission comparisons pass. Do not count either failure as a frame
pass or equate it with the separately unresolved native rectangle-input crash.
