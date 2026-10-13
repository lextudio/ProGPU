# Original raster shader references

These are unchanged original ProGPU shaders from
`8bfe5b885ae9a8dcceccdccb19bb9ebeacd487fb`. They are test resources only;
production imports the canonical shaders in `ProGPU.Backend`.

| File | Original source | SHA-256 |
| --- | --- | --- |
| Path.wgsl | PathRasterizerCommon.wgsl + one newline + PathRasterizer.wgsl | f92a1381881986ea28085550d1a838e92fd07f322e73067ccdf59c25a3c4662c |
| Glyph.wgsl | GlyphRasterizer.wgsl | eb70f351234bf4c02f9a591e88ebe6595492080c5da1cfe042d1eb2a2f079956 |

The path comparison keeps all 128 original geometry, fill-rule, sample-grid,
scale/phase and tail/rejection controls. The glyph comparison checks 384 cases
with original line/quadratic/cubic outlines, reversed/doubled/canceling/hole and
empty/degenerate contours, three scales, two phases and eight widths. It compares
all 44,596 words, including row padding, guards, zero-width outputs and packed
tails after overdispatch. Each reference and candidate runs on the same device.
No expected pixel is derived from the changed shader or corrected after readback.

These source compute checks do not replace native-provider, fragment/RGB,
full-package, image or application qualification.
