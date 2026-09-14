# Suntrail delivery and remaining work

Updated 2026-09-14 for [PR #158](https://github.com/wieslawsoltes/ProGPU/pull/158).
This plan separates the usable 2.5D sample delivered by that PR from the larger
game and engine requested afterward. Completion means demonstrated behavior on
Desktop, iOS and Browser, not merely a build or a source importer that retains data.

## Mergeable 2.5D slice

The shared Suntrail game and platform hosts provide eight original scrolling worlds,
optional vault routes, paired entry/exit pipes, pickups, enemies, checkpoints,
touch/keyboard controls and a Suntrail level workshop. The SMBX workshop reads and
preserves bounded LVL (versions 0–64) and LVLX source, offers geometry/layer/warp
editing, package export and custom artwork preview. Its playtest deliberately uses
Suntrail movement and only a documented subset of source geometry and local warps.
The source workshop is **not** a compatible SMBX or Mario game runtime. All default
art and routes are original; commercial assets and extracted levels are not bundled.

The procedural material cache and opt-in scene renderer live in `ProGPU.GameEngine`;
the game registers its drawing extension through the public factory API merged in
PR #159. Experimental world/depth/instance switches stay off by default until they
improve the same final phone build with acceptable memory and output quality.
The latest installed phone build predates this merge slice. A successful device build
or install does not demonstrate smooth frame pacing or visual/control quality.

## Next deliverables, in priority order

1. **iPhone performance and controls.** Install this exact signed Release source on
   a connected iPhone. Capture sustained gameplay p50/p95/p99 frame intervals, CPU
   submission, GPU time, allocations and Metal residency in every biome and vault;
   compare paired final binaries and export useful Instruments tables before deleting
   raw traces. Check two-finger stick movement, moving thumb feedback, full-height
   jump, pause/resume and device loss on the screen. Rework persistent scene/chunk
   ownership, visibility, demand-driven material preparation and upload only where
   profiling shows cost. Re-test output at native Retina resolution, including
   animation, translucency and scrolling. Target stable 60 FPS where the device and
   full-resolution quality budget permit; report actual percentiles rather than a
   nominal FPS claim.
2. **Campaign quality and game feel.** Review every route on device and Desktop,
   including vault entry/exit and checkpoint recovery. Add play-tested branching,
   vertical rooms, dungeons, pipes, encounter families, enemy behavior and authored
   landmarks. Tune jump windows, moving supports, hazards, rewards and accessibility
   with human play sessions as well as deterministic input-route checks. Make each
   world visually distinct with additional original procedural materials, lighting,
   animation and VFX while measuring overdraw and phone memory. The present stylized
   artwork is not a verified AAA or photorealistic result.
3. **Mario-family format and gameplay compatibility.** Build separate, bounded
   adapters and conformance fixtures for NES `.nes`, SMBX legacy `.lvl`, LVLX,
   SMBX-38A/other variants and their episode archives, plus the existing finite
   TMX/TMJ path. NES ROM support needs explicit mapper, bank, level, metatile and
   character rules; do not infer them from a filename. Complete non-UTF-8 legacy
   encodings, source-specific graphical/config resolution and unsupported-feature
   reports. Add source-specific player physics and validated block/NPC/event/layer,
   section, exit and warp behavior. Keep unknown script/data bytes intact on import
   and export. Use user-supplied art only; license and test representative files
   supplied by their owners. Claim compatibility per tested dialect and behavior,
   rather than “all Mario levels” from a parser alone.
4. **Full in-game authoring.** Extend the drag-and-drop workshop to all supported
   format objects, properties, world connections, layers, events and per-format
   resource references. Add conflict-safe episode editing, save/load and playable
   preview with exact round trips where promised. Confirm touch and mouse hit
   testing, scroll, undo/redo and exported package reopen on all hosts.
5. **Reusable engine contracts.** Keep game-specific world rules outside the engine.
   Expand the retained scene, spatial/collision, asset and input seams only when
   Suntrail needs them. Audit each shared rendering change for both managed and
   native applicability; pair implementations and measurements when it applies to
   both. Keep one canonical shader source per GPU algorithm and the typed, bounded
   public extension path available to NuGet consumers.
6. **Full 3D mode — last.** After the 2.5D game and importer are stable, add a
   switchable depth axis with actual 3D mesh/material/camera, collision and input
   semantics, editor representation and level interpretation. Validate gameplay,
   image quality and performance in both modes on Desktop, Browser AOT and iOS.
   The existing depth-buffered 2D world pass does not satisfy this item.

## Validation and release gates for later work

Each later deliverable needs focused simulation/source-format and shader audits,
Desktop Release tests, Browser AOT publish and interaction, signed iOS install and
visual input check, clean-room provenance audit, and matched final-binary performance
evidence. Keep exported metrics and needed review captures; remove unneeded raw
performance traces. The research and algorithm records for the current renderer are
in [suntrail-engine.md](suntrail-engine.md); SMBX scope and explicit limits are in
[suntrail-smbx.md](suntrail-smbx.md).
