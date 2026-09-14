# Native MIL text size at display DPI

## Acceptance path

`ProGPU.Wpf.ShowcaseApp` opened in `NativeMilWgpu` mode at 2× macOS display
scale exposed glyph ink rendered at about half the intended width and height
while WPF control positions and glyph advances stayed at their logical sizes.
Menus, headings, buttons, and body text therefore appeared letter-spaced and
undersized. A live window capture reproduced the issue before the change and
showed normal-sized, normally spaced text after replacing only the native
library. The before/after captures are 1656×1312 pixels, with SHA-256
`b7b30034f4b76e401cc4f29ed6fe5936195f4b19454eb4dded0f486e1e8fca63`
and `492599e227c5538958b27f88edbd71a30e630dca61df3094b81ed3e8ff647570`
respectively. They are local application evidence, not a Windows WPF
differential or final package gate.

## Contract and correction

The C++ MIL compiler rasterizes an outline at `em_size × dpi_scale ×
transform_scale`, subject to the existing 4–128 physical-pixel clamp. Its
positioned glyph carries `atlas_to_logical_scale` to the shared `Text.wgsl`
shader. That shader already divides atlas pixel coordinates by the frame DPI
before applying the ratio. The MIL compiler previously supplied
`em_size / raster_size`, dividing the glyph quad by DPI twice. It now supplies
`em_size × dpi_scale / raster_size`, so the shader produces the source logical
em size for both unclamped and clamped raster sizes. Source advances, positions,
physical raster resolution, hinting phase, transforms, and text style remain
unchanged.

The existing MIL high-DPI fixture now asserts the complete shader-scale
identity `atlas_to_logical_scale × raster_size / dpi_scale == em_size` for
1×, 2×, 2.625×, and 3× cases, including a clamped large font. This catches
the previous wrong ratio without a renderer-specific WPF workaround.

## Qualification boundary

The focused native MIL CTest and the live 2× Showcase source-overlay capture
pass. Full ProGPU CI, exact merged package use, native Xceed DataGrid, and
Windows/Linux application comparisons remain required before downstream
LibreWPF merge. Other control layout and glyph-shaping contracts are not
inferred from this scale fix.
