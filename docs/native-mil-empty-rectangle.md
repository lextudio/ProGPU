# Empty source rectangle commands

Toolkit filter focus exposed a static DrawRectangle containing WPF's exact
Rect.Empty wire value: positive-infinite X/Y and negative-infinite width/height.
This is authoritative empty content, not an invalid layout extent or a zero-area
rectangle. The original ProGPU implementation now preserves the record through
the managed MIL writer and handles it in the shared C++ MIL compiler before
float conversion. Nonzero brush/pen handles are still validated. It produces no
paint or geometry and does not remove surrounding scopes, owners or descendants.

Only the exact four-component sentinel is admitted for static DrawRectangle.
NaN, partial sentinels, infinite positive extents and finite negative sizes remain
errors. Zero-width/height rectangles keep their existing pen semantics. Animated
rectangles, other rectangle-bearing commands and spatial allocations are separate
contracts; this change does not generally permit infinite rectangles.

LibreWPF's managed render-data decoder uses the same managed sentinel predicate
to apply the empty draw without ink, before resource-dependent brush mapping.
Its typed primitive and legacy sink routes both preserve later zero-width draws.
The native route keeps the real record and C++ resource validation. No type-name
filter, managed input-index fallback, geometry approximation or CPU kernel was
introduced. This constant-size transport/validation change has no SIMD workload.

## Evidence — 2026-09-14

- Both native provider libraries and the managed backend build successfully.
- All 19 native CTest suites pass, including raw MIL exact-empty/malformed cases,
  invalid handle rejection, balanced opacity scopes and descendant traversal.
- All 201 LibreWPF native compiler/decoder tests pass in the diagnostic source
  graph, including four new managed/native-provider cases retaining point policy,
  other visual owners and zero-width drawing.
- The same unchanged Toolkit live gate passes rectangle translation and reaches
  a separate UnsupportedCommand in recorded native hit-index construction.
  Toolkit and final exact-package application qualification remain open.

The source graph uses explicit current assembly/native overlays; it is not NuGet
package evidence. ProGPU Build 34811802371 passed at a8afeab6, which predates these
changes and the continuation reflow commit. Fresh exact-head CI remains required.
