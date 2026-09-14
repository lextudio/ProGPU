# Sequential TileBrush page reuse

Toolkit filter focus emitted the same repeated tile owner twice, with identical
content revision and 20x20 extent. Cache preflight correctly rejected the second
nonshared owner. Only two pages (3,200 pooled bytes) existed: neither the 16-page
bound nor the 256 MiB aggregate budget was exhausted.

Repeated native MIL ImageBrush, DrawingImage, DrawingBrush and VisualBrush
captures now opt into the existing CACHE_SHARED contract. Their unchanged key
contains scene/brush identity, DPI, normalized mapping, page and content extents,
sampling and source render options; the content revision includes source resource
generations. Final opacity, placement and tile address mapping remain on each
composite. This shares identical captures, not independent paint operations.

The existing native cache budget and executor remain authoritative: all consumers
must opt in; content revision and extent must agree; recursive active-page use,
effects on shared pages, invalid identities and exhausted budgets still fail.
No pool limit increase, cache disabling, extra source capture or CPU work was added.
This is original ProGPU MIL producer wiring into its existing shared-page service.
Managed rendering is unchanged; it already owns its retained brush captures.

## Qualification — 2026-09-14

The native GPU fixture now covers four source kinds, all four repeating/flip tile
modes, and one/two 50%-opacity paints: 32 cases and 64 cold/warm renders. Every
pixel is checked against an independent nearest-sampling oracle. Each cold case
requires one content pass even with two paints; each warm replay requires zero
content passes and byte-identical pixels. The Metal Apple M3 Pro run passes.
The fixture is in both normal and image-brush-only/Windows-software test routes.
Final-head Windows/Linux and package qualification remain required.

The unchanged diagnostic Toolkit advances through filter text, popups, document
and anchorable menus, editors/resources, wizard/dialogs, zoom/scroll/panels, grid,
collection controls, themes/options, document activation and keyboard navigation.
It next fails native scene compilation at AvalonDock auto-hide overlay. This is
not full Toolkit or exact-package success. Artifacts under the external
progpu-core-release.xtwndj directory: toolkit-diagnostic-cache-preflight-details.log
and toolkit-diagnostic-shared-tile-pages.log. Temporary native diagnostics were
removed before the latter run; ordinary guards and deadlines are unchanged.
