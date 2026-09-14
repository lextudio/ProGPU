# Suntrail remaining work

The current prioritized scope and release gates are in
[the delivery plan](suntrail-plan.md). The dated notes below record how the
implementation evolved; earlier "not run" statements describe their original
batches and are not the current validation status.

User-requested scope, updated 2026-09-05. PR: https://github.com/wieslawsoltes/ProGPU/pull/158.
Items remain open until implementation and representative validation are complete.

1. Finish responsive iPhone touch controls: floating/fixed thumbsticks, arrow buttons,
   configurable sprint and button sizes, simultaneous movement/jump, equal jump physics.
2. Measure real iPhone Release performance, identify GPU/CPU bottlenecks, optimize without
   reducing artwork quality, and verify sustained frame pacing on the installed game.
3. Replace repetitive campaign geometry with distinct authored encounters per world:
   branching routes, vertical rooms, dungeons, pipe travel, and varied moving hazards.
4. Support loading NES Super Mario Bros. `.nes` level data, TMX/JSON tile maps, and
   SMBX `.lvl`/`.lvlx` levels. Implement independent format adapters, explicit unsupported
   feature diagnostics, and fixture-based compatibility tests. Support user-supplied
   Mario artwork/character data; do not bundle extracted commercial game assets.
5. Add an in-game drag-and-drop level editor with selection, placement, movement,
   deletion, undo/redo, save/load, and play testing. Integrate the supported Mario
   level formats and preserve format-specific data where round trips are supported.
6. Validate Desktop, Browser AOT, and iOS; reinstall on the iPhone, update PR and evidence,
   and remove unneeded raw performance traces after exporting useful measurements.
7. **Last:** add a switch between 2.5D and full 3D gameplay. This means a genuine depth
   axis, 3D camera/rendering and collision/input behavior, not a perspective filter.
   Validate controls, level interpretation, and performance in both modes on all hosts.

The iPhone black screen was fixed and the user confirmed gameplay. Smooth iPhone FPS
is not yet verified. The controls pass targeted input tests and native visual checks. Eight optional vaults,
two-way pipe travel, and three timed hazard families are implemented and tested.
The main campaign still needs a broader encounter redesign; new rooms do not by
themselves complete that request. Current sample validation: 97 Release tests. The reported unresponsive joystick was
reproduced through platform pointer injection and fixed in the shared WinUI panel
hit-test path; two-axis thumb feedback now repaints during dragging.
Full Mario format compatibility, editor completeness and full 3D remain open.

Next rendering investigation: retain expensive static procedural materials in a bounded
GPU texture cache, while keeping animated foliage, lights, and other changing effects
dynamic. Any such change needs physical-resolution and device-generation keys, bounded
residency, correct invalidation, image comparisons, and matched iPhone Release traces.
The six-pipeline specialization improved the measured fragment cost but is insufficient.

An opt-in full-precision sky cache and fixed-input GPU latency benchmark are now
implemented. Exact pixels pass across worlds, vaults and Retina scales. The cache
remains disabled by default because its memory cost and missing device validation
do not yet justify enabling it on iPhone. Device work stopped after installing
the joystick fix (`29366d4f`), as requested; further device validation awaits reconnection.

Exact-zero coverage rejection now skips invisible sphere/canopy/mountain lighting
without changing compared pixels or resource cost. The fixed-input benchmark supports
all eight worlds and explicit baseline/coverage options. This is an incremental
shader improvement; it does not close the smooth-iPhone-FPS requirement.

The pause-action hover defect is fixed by explicitly applying the existing Fluent
button style. Routed mouse/touch tests and the native reproduction pass.

All eight main routes now have authored section widths, elevations, gaps and
encounter sequences, including main-path tunnels, brambles and timed mechanisms.
Ordinary-input completion passes. Further distinct mechanics, branching-room travel,
format compatibility and editor support remain open; this is not a completion claim
for the expanded world-diversity request.

The user reconnected and requested installation. The authored-campaign/menu revision
with early coverage is installed and launched on iPhone (2026-09-05 11:48 local).
Device work is authorized again. Smooth FPS and real-device control feedback remain
open; this launch alone does not close those requirements.

The first workshop now supports mouse/touch palette placement, selection and drag,
one-step drag undo, redo, deletion, width edits, biome changes, bounded JSON save/load
and isolated playtests. Original finite Tiled object-map JSON/TMX adapters are tested;
At that stage tile layers, NES and SMBX data, connected custom pipes, asset import
and robust format round trips remained open. Desktop drag and playtest were visually
checked. Browser AOT and the application-owned extension API integration now pass.

Public drawing-extension registration was split into PR #159 and merged into main.
This branch merged main and uses the typed API across the game, GPU fixtures and
measurement tools. A procedural-pixel regression covers mobile surface recreation.
This completes the API extraction/integration request, independently of the open
gameplay, compatibility, editor and full-3D scope above.

Finite Tiled tile layers now compile embedded gameplay classes from JSON arrays,
TMX CSV/XML and raw/gzip/zlib base64. Matching solid runs coalesce into bounded
rectangles; independently authored and randomized fixtures verify occupancy,
format equivalence, corruption limits and ordinary-input completion. External
tileset/asset bundles, infinite maps, zstd, NES and SMBX remain open.


The user requested implementation batching and deferred broad validation until the
end, with rendering optimization and an updated iPhone installation first. The
current device pass prioritizes measured rendering changes and focused image/signing
checks; broader platform suites and feature validation are deferred until that final
validation phase. Connected-room authoring is now implemented in the working tree; its regression and UI validation are queued for the final validation phase.

World-specialized shaders are now enabled by default on iOS and installed in normal
play. Three nominal-temperature pairs reduced median frame intervals by about 16%;
a later Fair-temperature pair did not retain that frame-rate gain. Matched paused
GPU traces show about 8% lower fragment time. Sustained smooth FPS remains open;
this closes the current optimization/install pass, not the full performance goal.

## Reusable engine scope (September 5 steering)

- [ ] Complete the separate `ProGPU.GameEngine` material residency/compiler foundation and migrate Suntrail rendering, with measured iPhone improvement and visual quality. See [design and research](suntrail-engine.md).
- [ ] Add only scene/chunk, asset and input/simulation boundaries required by Suntrail, keeping game content outside the engine.
- [ ] Full 3D remains last, including meshes/depth, material channels/mips, camera, controls and collision; do not label orthographic sprite rendering full 3D.


The first reusable material engine is implemented, measured and installed on iPhone.
It reduces median frame interval about 26% and paused fragment time about 46%, with
190 MiB bounded material residency and zero visible page fallbacks in the measured route.
Smooth 60 FPS is still open. Next rendering work should address overdraw and persistent
GPU scene/instance preparation, with predictive asset preparation where justified.
Full scene/depth rendering, 3D materials and gameplay remain later steps, not completed
features of this first library. Broad validation stays deferred until the requested end phase.


## Connected custom rooms (implementation batch)

The document, game session, serializer and workshop now share an eight-room trail
model. Version 2 files preserve each room's biome/dungeon state and paired pipe links;
version 1 still loads. The editor supports room switching/add/delete, two-click pipe
linking, whole-trail undo/redo and automatic unlinking on deletion. Runtime room
instances retain pickups/enemies, and respawn restores the checkpoint room. Custom
dungeon exits are visible and complete the trail. The HUD follows actual room
identity, including rooms sharing the same biome. A three-room original example
provides an editable orchard/vault/skybridge loop.

Added regression cases cover complete-file round trips, legacy loading, graph
rejection, undo, endpoint deletion and room-preserving travel/respawn. These tests,
builds, real mouse/touch UI review and full route reachability have **not been run**
for this batch, following the user's deferred-validation instruction. This work is
newer than the final iPhone install; it has not been installed on the disconnected
phone. It does not finish Mario imports, campaign encounter diversity, smooth FPS
or full 3D.


## Surface mechanics (implementation batch)

Four mechanics now have simulation, original procedural artwork, workshop tools and
native document/import kind mappings: spring launches, directional conveyors, ice
braking, and contact-triggered collapsing ledges. Authored sections use spring
shortcuts in the orchard/highlands/sky, conveyors in aqueduct/coast/forge, ice runs
in the glacier, and collapsing upper routes in caverns/coast/highlands/sky.
The engine owns reusable contact response and fixed-tick support state; game tuning
and collision remain in Suntrail. Springs retain automatic launch height without
holding jump, so mobile jump-release behaviour does not weaken the spring launch.

Added regression cases cover response convergence, ice braking, exact support-cycle
boundaries, automatic launch height, idle conveyor transport, support loss/respawn
and authoring round trips. Tests/builds/ShaderResourceTests, visual review and route
validation are deferred and have not been run. No new FPS or all-level completion
claim is made. These changes have not been installed on the disconnected iPhone.


## Game-owned depth rendering (implementation batch)

The reusable engine now has a bounded full-resolution scene render target. Suntrail
can opt into opaque-first depth rendering with reverse GPU instance reads, original
painter-order translucent/live artwork and one scene resolve quad through the public
extension API. It uses the existing material buffer and canonical shader; no core
API changes. The target retains host MSAA and refuses oversized requests rather than
reducing quality. Extra render-target memory and submission/resolve cost are explicit.
Desktop and device measurement switches/counters are wired for paired final-binary
runs. The feature remains off by default and is not installed on iPhone.

Regression source covers budgets, reuse/resize, all-world comparisons, stable scene
replay and dynamic invalidation. Execution, shader validation, expanded cold-cache,
UI-overlap and device-recreation checks, image review and Instruments measurements
are deferred. This is an implementation milestone, not a measured speedup or smooth
FPS claim. Persistent world-space instance preparation and predictive material
preparation remain open architecture work alongside Mario imports and full 3D last.


## External Tiled resources and room packages (implementation batch)

The engine now provides an owned, bounded `AssetBundle` with relative path resolution,
ZIP entry preflight, CRC validation and no extraction or implicit file/network access.
The Tiled reader handles external TSX/TSJ/JSON definitions, cached once per unique
source path per map, and recognizes `.tmj` files. The shared picker opens ZIPs on all
hosts. A package manifest can assemble up to eight maps into one room graph using
Tiled `suntrail.pipeLink` object properties and `suntrail.dungeon` map properties.
Single-map ZIPs open without a manifest; Finder metadata is excluded from discovery.
Saving preserves the compiled room graph in the native v2 document.

Independent tests are written for XML/JSON equivalence, supplied resource snapshots,
relative/missing references, graph packages, enclosing folders, ambiguous maps,
duplicate names, directory bounds and CRC corruption. They have not been executed;
all-host picker use, final importer fuzzing and gameplay validation remain deferred.
Image artwork import, Tiled templates/infinite maps/zstd, NES and SMBX compatibility
remain open. This implements external gameplay definitions, not a claim that all
Mario/Tiled formats or source-art rendering are complete.

## Shared source/page transport (implementation batch)

The engine now has a fixed-capacity `RetainedGpuBuffer<T>` that writes one enclosing
changed range and preserves identical data without uploads. Suntrail's opt-in
shared-instance path stores each source once, with smaller page references, and
supports both painter and depth rendering through the original canonical artwork
functions. Desktop and iOS measurement drivers expose independent shared-instance
and world-pass switches, plus source/page upload and write counters. Default
normal-gameplay settings remain the previously measured renderer.

Added regression source covers transport layout, exact floating-point bits, owned
shadows, growth/shrink, stable allocation, all-world pixel equivalence, cold fallback,
mode changes and device recreation. Builds, test execution, shader validation,
Browser AOT and final native performance/image gates remain deferred and unrun.
This reduces duplicated instance representation by design, but is not yet a measured
FPS improvement. The latest iPhone installation still predates these changes.
Persistent world-space scene identities, visibility/chunk retention and predictive
material preparation remain open; source reindexing can still rewrite page data.

## Camera/scene preparation and bounded decoration visibility (implementation batch)

The engine now provides `Camera2D`, original `SpritePlacement` records and
`VisibleCellRange`. Suntrail retains world coordinates, parallax factors and
static/dynamic packing hints alongside its projected reference sprites. An opt-in
scene mode projects source bounds on the GPU and packs changing source records
behind static records while preserving page painter order. Long ground segments
select visible decoration cells before hashing/recording, with original seeds and
conservative edge footprints. Normal gameplay still leaves the experimental shared,
scene and depth modes disabled.

Regression code covers projection, all-world placement reconstruction, static
upload bounds, GPU scrolling/order comparisons and visibility-range properties.
Builds, tests, shader checks, screenshots and performance traces remain deferred
and unrun. The shared frame uniform is now 304 bytes, with all game/bake bindings
updated; earlier device measurements apply to the earlier binary. Full persistent
world entity IDs, chunk caching and predictive assets remain open, as do the broader
Mario compatibility, editor, smooth iOS performance and full-3D requirements.

## SMBX source preservation (implementation batch)

An independent LVLX document reader now retains exact source bytes, unknown fields
and sections, nested events, scripts, field order and source locations. It supports
bounded transactional edits of existing fields while preserving unrelated text.
The grammar and original fixtures are based on the maintainer's format specification;
see [SMBX design/provenance](suntrail-smbx.md).

Tests have been written but not run. This is the source-document layer only: the
game picker still does not import SMBX gameplay. Legacy LVL/38A decoding, runtime
section/object/behavior mapping, artwork, source insertion/deletion and in-game
source-format authoring remain open. No existing format support was removed and
this does not complete the user's all-format compatibility request.

## Legacy SMBX source reader (implementation batch)

The source-document model now reads the documented sequential LVL versions 0–64,
including conditional NPC/generator fields, warps, liquids, layers and classic-event
slots. It retains raw legacy encodings and supports transactional existing-field
edits using the legacy grammar. Independent source tests cover version boundaries,
field alignment, literal multiline strings and malformed edits; none has run yet.

This still does not enable SMBX gameplay in the picker. Runtime object/section
semantics, artwork, source editor integration, insertion/deletion, non-UTF-8 legacy
input, canonical export and SMBX-38A remain open. Final builds and validation are
pending. The rendering architecture implementation is ready for its focused final
build/image/performance phase before any new iPhone installation; broader format
and gameplay validation remains deferred until its implementation is finished.

## Rendering build and first comparison gate (September 5)

Desktop Release compiled successfully. The 55 focused world-pass/shared-instance/
scene-camera/visibility tests passed, and the 19 ShaderResourceTests passed.
All-world rendering comparisons include native-DPI 3 cases and MSAA 1/4 cases;
these are renderer surfaces, not a complete UI-overlap/device-lifecycle gate.
The editor status label now initializes before callbacks capture it, eliminating
its new nullable warning. Broader gameplay and importer tests remain deferred.

iOS Release also compiled with the four existing linker-analysis warnings and no
errors. Strict signing verification passed and all 250 required WebGPU exports
remain present. CoreDevice reports the phone unavailable, so this new binary has
not been installed or measured on iPhone. Experimental renderer modes remain off
by default; the previously installed material-page version remains on the phone.

Twelve same-binary Mac runs (three per mode, 120 warmup + 600 deterministic frames,
2796×1290, DPI 3, MSAA 4) are retained under
`artifacts/suntrail/performance/scene-architecture/mac`. Median run p50 serialized
completion was 6.816 ms for existing material pages, 6.637 ms for shared records,
6.911 ms for GPU scene projection, and 5.741 ms with the depth world pass. These
are serialized latency observations, not display FPS or iPhone results. The depth
path also increases CPU submission p50 from 0.667 to 0.780 ms and Metal residency
from 285,392,896 to 421,642,240 bytes. Final native Instruments correlation and
phone measurements are still pending; no new mode is promoted on this evidence.

Native Metal/Time Profiler captures for the two comparison modes have now completed
and their verified tables are retained; raw bundles were cleaned up after export.
The CPU captures include startup/JIT and unresolved managed frames, and the GPU
windows are not normalized by completed game frames. This is supporting diagnostic
evidence, not a completed steady CPU/allocation gate. Phone availability was
checked again after profiling and remained unavailable. New iPhone installation,
phone frame pacing, UI overlap/lifecycle checks and the remaining goal stay open.

## Rendering UI gate and SMBX geometry editing (September 5 continuation)

The three new `WorldPassUiTests` passed. They compare the actual game menu, play,
paused, settings and dragged-thumb UI using RGBA/BGRA targets at DPI 2/3, resize
away and back, and recreate a GPU device while retaining the UI. Joystick dragging
repaints the thumb without rebuilding the unchanged depth world. The recreated
UI/device comparison was byte-identical; the other comparisons passed the tight
image gate. Captures and per-state errors are in `artifacts/suntrail/world-pass-ui`.
These tests do not replace real iOS lifecycle and sustained performance validation.

The SMBX geometry editor model now previews drags without reparsing, commits only
coordinate/size fields and keeps bounded delta undo/redo while preserving all
other source bytes. Source IDs, layer/script data and unsupported geometry remain
available. Regression source was added, with the format/editor implementation
batch still unbuilt/unrun as requested. Picker/board integration and playable SMBX
behavior/art remain outstanding. The most recent signed iPhone binary predates
this geometry-editor batch, and the phone remains unavailable for installation.

## SMBX workshop integration (implementation batch)

The main editor now has a header action for the SMBX workshop. LVL/LVLX file opening,
original-format save-copy, retained draft navigation, a double-precision camera,
viewport-culling, mouse/touch source-coordinate dragging, pan/zoom, section framing,
size controls and undo/redo are wired to the source geometry model. The actual
source IDs and unknown fields remain intact. Unsaved replacements have a visible
explicit discard action; picker cancellation/errors preserve the old editor.

New UI/input regression source covers negative global coordinates, drag preview and
commit, second-finger isolation, cancel, reopening an edited draft, pan/zoom and
independent document replacement. This batch remains unbuilt and unrun as requested.
It does not yet add/deletes records, offer palette insertion, load SMBX artwork or
make arbitrary SMBX files playable. Those requirements, the remaining formats,
full 3D, final cross-platform validation and PR completion remain outstanding.
The iPhone was checked during this turn and is still unavailable; no install occurred.

## SMBX palette and structural authoring (implementation batch)

Implemented blank LVLX creation and palette insertion for blocks, backgrounds,
NPCs, water zones and pipe warps, with version-aware legacy serialization. Parser-
owned insertion offsets distinguish true list delimiters from field text. Object
and whole-warp deletion, guarded source-splice undo/redo, selected-record restoration
and mixed structural/field history now share the bounded source editor history.
The UI exposes an object ID, palette drag/drop, tap stamping, placement preview,
select/pan modes and delete. New/open discard actions remain separate.

Added original regression source for all 65 legacy versions, missing LVLX sections,
legacy message text that equals a separator, mixed move/add/delete undo, conditional
NPC records, fixed scaffold protection, warp deletion and routed mouse/touch palette
placement. No build or tests ran for this batch. Artwork and runtime compatibility,
section/player tooling, source properties, remaining formats, genuine full 3D and
final platform/PR gates are still open. The full goal remains active.

## SMBX artwork preparation (implementation batch)

Added reusable bounded PNG/GIF/BMP CPU decoding through the existing StbImageSharp
dependency, owned RGBA pixels and explicit binary-mask conversion. Added level /
episode custom-art resolution, deterministic image preference, case-collision
rejection, a 32 MiB retained pixel budget, NPC configuration preservation and
explicit simple frame-sheet selection. Pack preparation retains source bytes,
deduplicates image requests and reports missing/unsupported artwork independently.
Primary historical documentation and compatibility limits are recorded in
`suntrail-smbx.md`; no commercial artwork or upstream implementation was imported.

Original asset/config/package regression source was added. No build or tests ran
for this batch. Workshop ZIP selection and actual sprite drawing, base object
configuration, playable source-format behavior and the earlier outstanding work
remain open. The phone again reports unavailable, so no newer installation occurred.
The prepared signed iOS build predates this asset/editor batch; experimental
shared-instance/scene-projection/depth modes remain off pending iPhone measurement.

## Package selection and retained map drafts (implementation batch)

Connected bounded ZIP source packages to the SMBX workshop, with a level picker,
explicit activation, source/art preparation before switching, and retained editor
instances/undo for up to eight open maps. Dirty tracking covers inactive drafts;
replacing the package guards all of them. Saved drafts can be closed explicitly
to release a slot, and reopening then reads the original ZIP entry. Save-copy
remains a standalone source export, not an episode ZIP export. The package control
row scrolls horizontally on narrow screens.

Added artwork refresh from the current edited source and original model/UI
regression source for map switching, failed imports, retained history, saved
revisions, source preservation and draft limits. This batch is unbuilt/unrun as
requested. Actual sprite display on the board, complete package export, base
configuration/runtime compatibility and the earlier goal requirements remain open.

## Artwork inspection and extension frame context (implementation batch)

Added a selected-image inspector and NPC frame stepping when supplied metadata
defines the sheet. Unknown layouts show the whole source sheet with an explicit
label. A compositor-owned typed drawing extension retains bounded GPU images and
uniforms, samples within frame edges and applies accumulated opacity. Existing
projection and opacity getters are now public for external extension consumers;
their calculations and the native ABI are unchanged. Research/applicability and
the canonical shader's cost/ownership contract are recorded in the engine notes.

Added regression source for source-frame isolation, opacity, resize, RGBA/BGRA,
MSAA, stable uploads, device recreation and resource release. This batch is not
built or tested, and ShaderResourceTests remain part of the deferred final gate.
The board still needs actual sprite placement and base object rules; an inspector
does not establish playable SMBX compatibility. The full goal stays active.

## Episode export and saved package baselines (implementation batch)

Added bounded episode ZIP export containing all original package files and every
open draft's committed source edits. Output uses deterministic ordering/metadata;
file bytes are preserved, while original archive metadata/compression is not.
Only successful file writes advance the episode baseline and saved revisions.
Closed drafts reopen from that baseline and remain included in later exports.
Canceled/failed saves, pointer previews and older asynchronous completions cannot
overwrite newer committed save state. Standalone source copies remain separate.

Added reusable immutable asset-bundle content revisions and bounded BCL ZIP output,
plus original regression source for binary/name preservation, file/ZIP limits,
multi-map edits, closure/reopening, save sequencing and cancellation. This batch
and its tests are unbuilt/unrun, as requested. Map sprite placement/base rules,
playable format compatibility, performance/device installation, genuine full 3D,
final platform gates and the draft PR still remain unfinished.

## Object definition metadata (implementation batch)

Added source-preserving bounded INI configuration reading and split object-index
resolution for caller-supplied block/BGO/NPC definitions. The workshop can load a
definition ZIP and inspect supplied dimensions by original object ID. Unknown
collision/shape/animation/algorithm data remains intact and is not substituted
with Suntrail behavior. Current official documentation was located on GitHub;
section artwork now prefers its documented background2-* name with the previous
spelling retained as a fallback.

Added independent regression source for config syntax/ownership, split indices,
typed dimensions, root limits and image-name precedence. No builds or tests ran.
This adds metadata inspection, not completed base-art loading or compatible
gameplay. Large/demand-loaded configuration archives, object behavior mappings,
map sprites, the other level formats, engine/performance work and final 3D/platform
and PR gates remain open.

## Definition archives read on demand (implementation batch)

Added a reusable indexed ZIP asset source and connected it to definition loading.
The source validates its directory up front and expands/checksums requested entries
only; it retains compressed content instead of all expanded assets. Limits now
allow larger definition packs while remaining explicit. Parsed definition caching
is bounded by entries, source bytes and fields, with cached invalid-file results.
Owned archives are released on replacement; the eager episode/export path remains
unchanged in scope.

Added original regression source for eager/indexed equivalence, large packs,
requested reads, deferred corruption checks, ownership, cancellation and cache
eviction. No builds or tests ran for this batch. The iPhone still reports unavailable;
no installation occurred. Base artwork integration, map sprites, runtime behavior,
other formats, rendering/performance, genuine 3D and final validation/PR work remain
unfinished, and the full goal stays active.

## Prepared iPhone build installed (2026-09-05)

The phone became available. CoreDevice successfully installed and launched the
prepared signed Release bundle; install and launch receipts are retained under
`artifacts/suntrail/performance/scene-architecture/`. This bundle includes the new
rendering architecture with its experimental modes disabled by default, and
predates the subsequent SMBX editor/artwork/archive implementation batches. The
installation establishes deployment only, not visual correctness, touch behavior
or sustained iPhone performance. Device measurements and final validation remain
open; the draft PR and the overall goal remain unfinished.

## Base definition artwork (implementation batch)

Connected caller-supplied definition images to level artwork preparation and the
inspector, after level/episode overrides. Added bounded portable path/name indexing,
explicit root-relative and unique-descendant lookup, mask source isolation and
owned prepared-pixel lifetime. Definition replacement refreshes the active map
without changing its source edits, revision or undo history. Base files remain
separate from exported episodes. Documented the adapter policy and primary public
contracts without importing foreign implementation or artwork.

Added regression source for resolution, ambiguity, boundaries, invalid overrides,
disposal and draft/export preservation. No builds or tests ran, per the deferred
validation instruction. Actual board sprites and compatible gameplay, performance,
campaign work, other formats, full 3D and final platform/PR gates remain open.

## Batched prepared-art renderer (implementation batch)

Extended the original artwork inspector extension to immutable painter-ordered
sprite batches, sharing one bounded texture cache with individual previews. Added
packed retained instance streams, adjacent-image run grouping without reordering,
per-owner target pipeline selection, bounded capacity growth and image pinning
before demand upload. The canonical shader keeps frame-clamped premultiplied
sampling while reading per-instance source and destination rectangles.

Added original regression source for ownership/bounds, many sprites, overlap
ordering, draw counts, growth/shrink, stable uploads and resource release. Recorded
primary research, original in-repository provenance, complexity and managed/native
applicability in the engine notes. No builds, tests or performance validation ran.
The board still needs semantic sprite layout and placement using this foundation;
compatible runtime behavior, other formats, campaign/performance work, full 3D and
final platform/PR gates remain open.

## Board sprite placement and picking (implementation batch)

Connected prepared images to the source board through a culled immutable sprite
batch. Added CPU placement for explicit static/vertical frame layouts and complete
NPC.txt sheets with supplied body dimensions. Unknown sizing/animation rules retain
editable source markers and report layout issues. Sprite selection follows visible
art bounds alongside original geometry, and drag previews reuse prepared metadata
and images until a source commit. The pack retains bounded prepared definitions
independently of the source archive lifetime.

Added regression source for frames, placement, unresolved layouts, sprite picking,
preview/cancel, commit/undo and detachment. Builds, tests and visual verification
remain deferred. Sizable tiling, semantic layers, full animation rules, compatible
runtime and the wider performance/campaign/format/3D/platform/PR requirements remain
unfinished.

## Tiled sizable-block sprites (implementation batch)

Added static 96×96 nine-part sizable artwork on the editor board, rendered as one
quad with fixed-cost GPU coordinate remapping. Corners keep their size and edges/
center repeat without per-tile geometry or stretched artwork. Added explicit size
and precision limits and kept unsupported cases editable with layout issues.
Packed sprite records now include sizing data; all sample hosts share the canonical
shader. Recorded original provenance, cross-engine reasoning and applicability.

Added independent CPU/GPU regression source for placement counts, sizing limits,
corners/edges/center, partial tiles, ordering-compatible batching and retained
uploads. No builds or tests ran. Full semantic layers, animation/behavior rules,
other formats, campaign/performance work, genuine 3D and final platform/PR gates
remain unfinished.

## Source-layer editor visibility (implementation batch)

Added grouped source-layer controls with show/hide and show-all actions. Membership
is prepared from declarations and object references; hidden groups leave drawing
and selection together, with drag cancellation and hidden-selection cleanup.
Visibility remains editor state, survives source commits/undo by name and never
rewrites saved flags, source bytes or revisions. Added original regression source.
No builds, tests or UI checks ran. Semantic z-order, event-driven layers and the
wider gameplay/format/campaign/performance/3D/platform/PR work remain unfinished.

## Selected-object properties (implementation batch)

Added source-preserving object layer assignment and NPC direction controls with
bounded undo/redo, including guarded insertion of omitted LVLX fields. Legacy
edits remain limited to existing version slots. Fixed workshop keyboard routing
so focused text entries do not invoke map delete/pan shortcuts. Added original
regression source for insertion, encoding, unchanged source, history and legacy
slots. No builds or tests ran; full properties/runtime and the broader goal remain
unfinished.

## Accelerated coding sequence and editor transactions

The user reiterated the sequence: finish coding, then perform QA, then commit and
push. Continue larger implementation batches; do not treat individual untested
features as ready to integrate. The goal and draft PR remain unfinished.

This batch adds layer declaration creation, atomic renaming through documented
object and classic-event references, and pipe/door property controls with atomic
updates to type, directions, destination and flags. Legacy source slots and unknown
data remain preserved; scripts/custom event actions are not refactored. No QA,
commits or pushes ran. Runtime compatibility and event behavior, broader formats,
campaign/gameplay work, rendering/performance, full 3D and final platform/PR gates
remain open.

## Shared imported-world motion foundation (coding batch)

Added immutable indexed static collision generations, double-precision swept
rectangle movement and a configurable fixed-step character controller in the
reusable engine. Supports explicit solid/one-way contacts, buffered/coyote jumping,
jump release, drop-through and validated teleport placement. The existing Suntrail
session is unchanged; this foundation still needs imported section/object-rule and
game-view integration and does not establish playable SMBX compatibility.

No QA, commits or pushes ran. Runtime rules/sections/warps, broader formats,
campaign and rendering/performance work, full 3D and final platform/PR gates remain
unfinished.

## Imported geometry playtest and retained scrolling (coding batch)

Connected the new double-precision collision/character foundation to a launchable
workshop geometry playtest. It uses an immutable source snapshot, section-relative
camera bounds, Suntrail courier motion/art, respawn, validated local warp targets,
zero/preserved-momentum transfers, a grounded-entry requirement and cooldowns.
The explicitly labeled policy treats all block rectangles as solid. NPCs, zones,
events, layers, original block behaviors and external-level transitions are not
emulated; a bounded report describes unsupported records. This is not full SMBX
or Mario gameplay compatibility.

Added a shared playtest UI with pause/restart/edit return, keyboard press/release,
floating/fixed stick and arrow controls, large/standard buttons and persisted
sprint/layout settings. Main game and preview now share touch capture/cancellation
code; releasing one keyboard alias no longer releases another held alias in the
main game. Application deactivation pauses the preview and clears input. Standalone
source files can now prepare artwork from selected base definitions as well as ZIP
packages, with explicit sprite refresh after edits.

Source artwork retains a camera-relative batch with 192-pixel overscan and reuses
it through 96 pixels of translation. Content/visibility/zoom/resize changes rebuild
the snapshot. CPU-packed geometry is shared by immutable batch identity across
compilations/devices; opacity changes do not repack instances. These are coded cost
contracts, not measured performance claims. No builds, QA, commits or pushes ran.
Full imported behavior/formats, campaign improvements, sustained device performance,
full 3D and final platform/PR gates remain open.

## Campaign route diversity and vault connections (coding batch)

Replaced the repeated generic upper-gallery pass with encounter-specific terrain
and rewards. Added canopy forks, broken arches, single/twin ferries, collapsing
bridges, split causeways, saw slaloms, staggered flames, pressing conveyors, ice
braking pads, crystal stairs and sky relays. Each of the eight overworld scores and
eight named vault scores now selects a distinct sequence and silhouette. Relics
stay at three authored overworld encounters; vault coins remain persistent bonus
rewards. Narrow ground piers no longer sprout oversized trees/bushes.

Both vault pipes now connect to matching early/late overworld pipes, allowing a real
optional shortcut in either direction. Emerging at the later overworld endpoint
creates a safe respawn there. Custom room pipe graphs retain their separate path.
Added deterministic hopping and hovering enemy profiles, original horn/wing shader
variants, and floor-attached shadows. Native editor, JSON and Tiled classes support
hopper/hoverer objects. The input-only route pilot now considers moving and timed
supports when predicting landings, instead of rejecting all non-ground landings.

No tests, builds, screenshots, playthroughs, profiling, commits or pushes ran.
All new layouts, relic routes, enemy timing, pipe/checkpoint state and shader output
need the final QA pass. Earlier route/image/performance results predate this batch
and do not validate it. Full imported behavior/formats, remaining artwork/engine
work, sustained iPhone FPS, full 3D and final platform/PR gates remain open.

## Reusable spatial visibility generations (coding batch)

Added an immutable double-precision spatial hierarchy in ProGPU.GameEngine. It
orders centroids on a bounded interleaved-bit lattice, constructs a balanced tree,
retains exact conservative bounds and returns visible IDs in authored painter order.
Queries use a fixed traversal stack and reusable caller output, with no normal heap
allocation. This replaces a draft interval-only approach whose long overlapping
section bounds could force large prefix scans; no interval implementation remains
in this visibility component. The separate collision solver is unchanged.

Integrated it into native-world platform drawing/occlusion and SMBX artwork,
overlays and picking. Native bounds include full platform motion and artwork
extents. Imported bounds combine source geometry and prepared artwork; active drags
are included explicitly without rebuilding the source tree per pointer event.
Source/art changes prepare the hierarchy before drawing. Selection-only changes
retain the existing sprite snapshot. Overscan still permits camera-only uploads to
remain limited to transforms.

Platform arrays are now immutable. ReplacePlatform publishes changed platform data,
a rebuilt visibility generation, support reset and width bounds together; paused
GameSurface rendering observes its generation. Connected pipes still use complete
LevelDocument updates. Migrated existing test callers to this explicit mutation
contract; no tests or other QA ran. CPU/GPU performance improvements remain unproven.
No commits or pushes ran. The full goal, broader engine/import/3D work and final
platform, gameplay, shader, performance and PR gates remain open.
