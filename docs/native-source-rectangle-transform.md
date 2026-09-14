# Noninvertible source rectangle input

Toolkit filter focus contains a retained image input scope with rectangle
(0, 0, 19.9999, 20) and affine transform (0, 0, 0, 0, 227.799, 2772.94).
Rendering can omit collapsed geometry, but the recorded native input builder
rejected the scope when it could not produce an inverse mapping. That made the
complete native input index unavailable.

The original managed contract in
src/ProGPU.Scene/GpuRenderCommandHitTestCache.cs already guards image and point
rectangle scopes with IsFiniteInvertibleAffine2D. The corresponding native
rectangle append now treats an exactly singular, finite affine as no query
geometry, preserving scope restoration and following owners. It never uses an
identity inverse, invents a collapsed point or replaces the native index with a
managed one. Double determinant arithmetic avoids an epsilon cutoff and preserves
small invertible transforms. Existing finite/overflow validation remains in the
normal native placement path. This constant-size determinant is not a bulk CPU
fallback; existing intrinsic bound/clip transforms remain unchanged.

This applies to the existing rectangular source/image/point capture seam, not a
general waiver for unsupported clip topology, effect/cache frames or primitives.
It does not change BitmapCache.RenderAtScale=0 input ownership.

## Qualification — 2026-09-14

- Both native providers build. All 19 CTest suites pass; scene 9842 exercises
  zero, rank-one, tiny invertible and mirrored matrices for both rectangle scope
  policies, and verifies the next owner's restored coordinates.
- The paired SourceRectangleHitTestTests fixtures pass (2 tests, no skips) in a
  focused project linking the exact checked-in test file and current Scene project.
  The broad ProGPU.Tests graph could not compile locally because generated WinUI
  theme/sample prerequisites are absent; this is not a broad managed suite pass.
- The unchanged native Toolkit gate now completes input-index compilation and
  reaches rendering, which rejects retained cache ownership/capacity preflight.
  Artifact: toolkit-diagnostic-singular-image-input.log under the external
  progpu-core-release.xtwndj evidence directory. All temporary return/rectangle
  diagnostics were removed and both providers rebuilt before this run.
- The MIL coverage ledger was regenerated after the empty rectangle compiler
  change; all generated native contract/Unicode/memory inventory checks pass.

The application graph is diagnostic, not final NuGet qualification. Cache
preflight, complete Toolkit input, fresh exact-head CI and downstream package
qualification remain required before core delivery. Broader goal requirements
remain tracked separately.
