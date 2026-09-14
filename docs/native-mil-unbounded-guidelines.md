# Native MIL unbounded static guidelines

Acceptance: **ProGPU.Wpf.ShowcaseApp**, focusing and typing into the TextBox.
The package-mode native host rendered and resized, then rejected the caret
adorner's Y guidelines `[0, double.MaxValue / 2]` while serializing the visual.
This is WPF's real unbounded adorner layout, not a missing geometry descriptor.

The canonical visual guideline packet carries floats. Its very large finite
source coordinate becomes positive infinity. ProGPU now preserves positive and
negative infinite static anchors through the managed packet builder, native MIL
decoder, static scene builders and scene validation. Existing ProGPU nearest-
anchor search and rounding assign zero displacement to an infinite anchor;
ordinary finite anchors continue to win for finite points. No coordinates are
clamped, no visual types are filtered and source input geometry is unchanged.
NaN remains malformed. Managed NaN rejection happens before packet allocation.
Explicit dynamic-offset resources retain their finite-only contract. Mapping
that produces NaN still rejects rather than publishing invalid scene state.

Implementation provenance is the original ProGPU static guideline storage,
SIMD axis mapping and semantic snapping at `960dfbfb`. The WPF source was
consulted to establish the adorner layout/protocol behavior only; no foreign
renderer implementation was copied. Mapping retains its NEON/SSE2 loops and
bounded tail; ordered nearest-anchor search remains dependency-bound O(log N).
Serialization/validation is O(N), with the existing bounded resource storage.

Verification:

- Both macOS ARM64 native providers compile; all 19 native CTests pass.
- Native regressions cover positive/negative infinite anchors, mirrored axes,
  finite snapping, glyph placement, scene validation and NaN/dynamic rejection.
- All 124 linked `NativeRendererInteropTests` pass, including exact packet
  encoding, atomic NaN rejection and managed scene-state round trips.
- Generated native contracts and the regenerated MIL coverage ledger verify.
- A diagnostic overlay of the rebuilt provider and managed native adapter on
  the packaged Showcase gets past caret guideline compilation. It then rejects
  a separately surfaced popup target with `UnsupportedCommand`. This is not a
  passing package/app gate; final packages must be rebuilt without overlays.

Continue that popup dependency and final CI/platform/package qualification.
Stock Windows single-pass bounds execution remains separate from this fix;
ordered queries remain explicit, with no default-policy or merge-gate change.
