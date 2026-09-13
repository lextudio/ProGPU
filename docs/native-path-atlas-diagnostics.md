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
