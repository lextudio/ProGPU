# Native MIL path atlas capacity and snapped paths

## Acceptance path

`ProGPU.Wpf.XceedPaidApp` opens the licensed virtual DataGrid, renders it on
the C++ native MIL path, then scrolls and queries the current GPU hit-test
index. On macOS/Metal, the packaged LibreWPF application exposed two
successive failures in ProGPU main: the retained path set exceeded its 4096²
R8 atlas, and a per-point guideline collapsed one filled path's snapped X
bounds to a single float coordinate. The second path was admitted before
snapping but rejected by the raster frame validator afterward.

The path atlas may now grow to 8192² when necessary. Individual path tiles,
glyph atlases and clip atlases keep their existing 4096-side safety limits.
The WebGPU texture allocation remains checked and fails explicitly if an
adapter cannot supply the larger texture. No CPU or managed rendering fallback
is introduced. A snapped zero-area path keeps its actual unchanged segments;
only its tile descriptor receives the next representable maximum coordinate,
so the raster contract remains strictly positive without painting a new shape.

## Evidence and remaining qualification

An isolated package-output overlay replaced only `libprogpu_native.dylib`
with a local ProGPU build and rebuilt the Xceed acceptance assembly. The
unchanged native MIL renderer mode and native hit-test gate completed the live
validation: 1180×760 logical, 2360×1520 pixels at 2× DPI, full viewport,
loaded DataGrid, large-scroll budget and GPU hit testing. The acceptance
check now performs a real bounds query for each newly installed scene before
requiring device index residency; it does not infer GPU upload from CPU index
construction or relax the owner/count assertions.

This overlay is local evidence, not a published package or Windows/Linux
qualification. The final ProGPU CI, merged package pin, LibreWPF CI and
platform application gates remain required before merge admission.
