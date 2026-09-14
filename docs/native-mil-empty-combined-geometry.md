# Empty operands in native MIL combined geometry

## Application dependency

ProGPU.Wpf.ToolkitApp's AvalonDock auto-hide overlay emits a source geometry
clip whose postfix program is one rectangle leaf, an explicit empty operand,
then union. Native rendering accepts that program, but recorded native input
requires an admitted exact clip path and rejected the redundant Boolean program.
The observed leaf has four segments, even-odd fill and bounds
(715, 92.9961)–(975, 615.201). These bounds are diagnostic metadata, not a
replacement for the path.

## Shared compiler correction

`append_boolean_geometry` in `src/ProGPU.Native/src/Mil/progpu_native_mil.cpp`
now resolves both operands before applying exact empty-set identities for union,
intersection, XOR and difference. An affirmatively empty operand is distinct
from unavailable geometry or failed resolution. Invalid handles and unsupported
operands still fail rather than being skipped by short-circuit evaluation.

The surviving original segment range, postfix subtree, fill rule and already
composed transform remain authoritative. Compaction rebases leaf offsets; an
empty result removes its segments and publishes one canonical empty node. A
single surviving clip leaf drops its now-unnecessary program and carries that
leaf's actual fill rule into the ordinary vector clip contract. No hit-test
admission guard, GPU query shader, cache limit or source application assertion
is relaxed. Two real Boolean operands retain their original program.

Provenance is the existing original ProGPU geometry compiler and the matching
managed `PathOpGeometrySolver.ClassifyDeferredQueries` / `TryCreateImmediateResult`
and `PathAtlas.CompilePath` empty-operand behavior. Managed rendering already
implements these identities and needs no production algorithm change.

The added work is bounded operand metadata plus contiguous intrinsic `memmove`
compaction and a dependent postfix offset fixup. It adds no pixel fallback,
readback, P/Invoke or per-frame managed allocation. Complexity remains bounded
by existing geometry traversal and retained segment/program sizes; there is no
measured performance-improvement claim.

## Regression and delivery evidence — 2026-09-14

The native `empty_combined_clips_preserve_original_fill_and_scope` fixture tests
48 combinations: four operations, left/right/both empty, both source fill rules,
and direct/nested expressions. It compiles a real MIL hit index and checks exact
transformed clip bounds, original fill, empty results and the following unmasked
sibling. Both native providers build, all 19 CTest suites pass, and the generated
native contract, MIL coverage, memory inventory and Unicode verifiers pass.

`EmptyCombinedClipTests` checks the same 48 managed geometry combinations and
retained original fill encoding. All four parameterized tests pass in the focused
project linking the checked-in fixture against current ProGPU projects; the two
existing source rectangle tests also pass. This is not a broad managed suite or
final package qualification.

The unchanged diagnostic Toolkit gets past the redundant clip program and
rejects the native stroke-batch admission predicate during the same auto-hide
action. Its actual stroke descriptor is the next diagnostic target; this
correction does **not** qualify the complete overlay or
the remaining floating-window/lifecycle actions. Final exact-head CI, package,
Windows/macOS/Linux application gates and ordered merges remain required.
