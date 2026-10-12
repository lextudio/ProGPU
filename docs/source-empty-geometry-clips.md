# Empty source geometry clips

LibreWPF issue [270](https://github.com/wieslawsoltes/LibreWPF/issues/270)
exposed a managed source-input failure: zero-area clips and disjoint nested
clips threw while capturing an opacity-zero visual. Visible controls followed
the ordinary rendering path and remained alive.

Source capture now asks the existing path compiler for exact coverage and its
physical bounds. A successful empty compilation carries an explicit empty
result; unavailable input does not. The existing compilation cache retains that
result beside the original path data. Older cache providers default to an
unproven result, preserving their existing interface behavior.

Empty admission validates the complete original input, including both Boolean
operands, supported segments, finite coordinates and fill rules. This check is
needed because the general transform helper can omit unavailable input. Exact
zero-area or disjoint clips push an empty scope; popping it restores following
owners. Opacity, source visibility, curves, fill rules and original transforms
retain their independent meaning. No opacity-based input suppression, invented
bounds, epsilon rejection or raster fallback is introduced.

The additional validation costs O(F + S) time and temporary storage for F
figures and S segments only when proving empty coverage, with existing segment
limits and a bounded Boolean traversal. Cached compilation retains its empty
proof; source admission still checks the original untransformed input. Ordinary
nonempty compilation and cache keys are unchanged. This is a correctness fix;
no performance improvement is claimed.

## Native applicability and design references

The C++ MIL producer already resolves both original operands before applying
empty-set identities in `append_boolean_geometry`, and represents empty
coverage explicitly. Its `empty_combined_clips_preserve_original_fill_and_scope`
fixture retains both operand orders, transforms, nested programs and following
owners. It does not use the managed `TryGetBounds` precondition or the managed
cache's former ambiguous false result. Both native providers consume that same
producer, so no native algorithm or ABI change is needed for this failure.

The existing [source-input design record](native-mil-hit-test-ownership.md#design-references-and-decisions)
applies here. The primary API references were revisited on 2026-10-12:
[Skia paths](https://api.skia.org/classSkPath.html),
[Direct2D geometry](https://learn.microsoft.com/en-us/windows/win32/api/d2d1/nn-d2d1-id2d1geometry)
and [Win2D geometry](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_Geometry_CanvasGeometry.htm)
keep geometry, bounds and containment distinct. The fix adopts that separation
using ProGPU's own compiler. [WebRender](https://firefox-source-docs.mozilla.org/gfx/RenderingOverview.html)
and [Vello scenes](https://docs.rs/vello/latest/vello/struct.Scene.html)
inform retained ownership and balanced scopes. [Parley layouts](https://docs.rs/parley/latest/parley/struct.Layout.html)
and [HarfBuzz plans](https://harfbuzz.github.io/harfbuzz-hb-shape-plan.html)
remain independent: no text shaping, font fallback, variation, hinting or layout
policy belongs in this clip fix. Startup, worker preparation, GPU batching,
uploads, DPI policy and device/atlas invalidation are unchanged. No external
implementation was copied.

## Validation

After the fix, 57 source-visual controls and 152 related geometry/input controls
passed with zero skips. Added cases cover cached/uncached compilation, cold/warm
capture, opacity zero/one, degenerate/rotated/disjoint clips, following owners,
and rejection of missing, unsupported and nonfinite original geometry.

The issue's unchanged application and original three-second shutdown timer were
run in all eight modes. The qualified baseline reproduced all four reported
opacity-zero crashes; all four visible controls passed. Replacing only the two
locally rebuilt managed assemblies made all eight modes pass, with all other
application/framework/native files byte-identical. This is a local source
comparison against the successful LibreWPF PR274 build, not a public-package or
Windows desktop qualification. Whole CI, both provider/package gates and the
framework's final producer pin remain required before closing the issue.
