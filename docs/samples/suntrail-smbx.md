# Suntrail SMBX source compatibility

Current merge scope (2026-09-14): the source workshop reads bounded LVL 0–64
and LVLX, preserves unknown source bytes, edits geometry/layers/warp properties,
previews supplied artwork, exports episodes, and offers a Suntrail-physics
geometry playtest with a limited local-warp subset. The ordinary game picker
still does not run SMBX levels with SMBX/Mario mechanics. The sections below
are dated implementation notes; their earlier "not run" statements are
superseded by the current validation in [the delivery plan](suntrail-plan.md)
and PR #158. Remaining compatibility work is listed in that plan.

This is implementation work in progress. The game picker does not yet load SMBX
levels for gameplay. LVLX source parsing and field-preserving editing are now
implemented; the runtime object/section mapping, legacy LVL readers, source artwork
and behavior compatibility remain open. Do not interpret parsing as full support
for an SMBX episode, its scripts or its gameplay.

## Document layer

`Game/Import/SmbxSourceDocument.cs` reads the tagged LVLX text grammar into bounded,
immutable records. It preserves original UTF-8 bytes, BOM, line endings, whitespace,
field order, unknown fields/sections, nested event sections and script text. Record
fields retain source offsets and their exact raw representation. Typed accessors
read strings, finite numbers, integer identifiers and boolean flags. Strings support
the documented escapes and hexadecimal UTF-8 representation; arrays retain their
original form, including packed and textual boolean representations.

`WriteOriginal` returns an owned byte copy. `WithFields` replaces existing field
values transactionally, leaves all unrelated source text intact and reparses the
result. Edits are applied in source order in one pass. This is intended for save/apply,
not for every drag event. The API currently replaces existing values; insertion and
deletion of records/fields will be added for full source-format authoring.

Bounds are explicit: 8 MiB source bytes, 65,536 records, 262,144 total fields,
256 fields per row, 4,096 section openings, depth 16 and 4,096 edits per transaction.
Parsing is O(B*D + F), with bounded array depth D; storage is O(B + F). Editing is
O(B + E log E). All parsing occurs outside simulation/rendering. Merely recording
scripts and relative file names does not execute or resolve them.

The compiler above this layer still needs to validate section geometry, associate
objects with sections, preserve their source IDs and behavioral data, select the
matching artwork/physics rules and report features it cannot yet execute. That work
must not silently turn every block ID into the same solid or every NPC into the
same enemy. The source document will remain available for round trips even where
runtime compatibility is incomplete. Existing Suntrail v2 documents are unchanged.

## Primary specification and clean-room record

The format authority is Wohlstand's [LVLX specification](https://github.com/WohlSoft/PGE-File-Library-STL/blob/master/docs/PGE-X/LVLX%20file%20description.pdf),
updated December 17, 2024. The text grammar, marker names, field types, escaping and
section descriptions informed this original parser. One public upstream test-file
header was observed to confirm zero-based section SC and the tagged source form;
no upstream level layout, artwork or implementation was copied into the project.
Tests use an independently authored format yard and adversarial syntax cases.

The next legacy reader will use the maintainer's [SMBX1–64 LVL specification](https://github.com/WohlSoft/PGE-File-Library-STL/blob/master/docs/SMBX64/LVL%20file%20description.pdf),
updated March 2, 2020, for version-dependent sequential fields. Its implementation
appendix is not an implementation source. [Microsoft's Write statement contract](https://learn.microsoft.com/en-us/office/vba/language/reference/user-interface-help/writestatement)
confirms invariant decimal punctuation, boolean spellings and CRLF output, and
warns that embedded quotes do not round trip safely through Input. Do not assume
CSV-style doubled quotes or LVLX backslash escapes in legacy LVL strings.

This game-format layer is shared unchanged by the existing Desktop, iOS and Browser
projects. It changes no rendering, native ABI, font or shader contract. The generic
engine still owns assets/rendering primitives; SMBX interpretation stays outside it.

## Validation status

Regression source covers original-byte ownership, unknown fields and nested records,
UTF-8/BOM/CRLF, quoted and hex strings, transactional multi-field edits, malformed
syntax, duplicate markers, fractional integer IDs and resource limits. These tests
and all builds have not been run, following the requested deferred-validation pass.
No real-file import, source-editor UI, gameplay or full-format compatibility claim
is made for this document-layer batch.

## Sequential LVL source reader (implementation batch)

`SmbxSourceDocument.ReadLegacy` now reads the specification's versions 0 through 64
into the same source-record model. Section/player ordinal indices are explicit;
coordinates remain in the original global coordinate system. Header, section,
start-point, block, BGO, NPC, warp, environment, layer and classic-event fields
retain their original tokens and offsets. Conditional NPC special fields and
optional generator payloads are consumed only under their documented conditions.
Legacy block contents, unused fields and event slot arrays remain raw for later
behavior mapping. No unimplemented behavior is silently translated into another.

Legacy strings are literal quoted text, with actual multiline content preserved;
backslashes are not LVLX escapes. Embedded quotes fail explicitly rather than
pretending they use CSV doubling. Boolean tokens retain `#TRUE#` / `#FALSE#`.
`WithFields` chooses the corresponding grammar and reparses the complete edited
source, so an edit that would shift a version-dependent record is rejected.
`EncodeLegacyString` provides the matching literal string representation. Integer
identifier access uses integer lexical syntax, avoiding fractional rounding or
exponent underflow into a different identifier.

The current legacy byte decoder accepts ASCII/UTF-8 and retains BOM and input line
endings. LF-only input can be inspected, but `WriteOriginal` does not convert it to
SMBX's required CRLF output. ANSI/code-page input, canonical legacy export and the
separate SMBX-38A dialect remain open compatibility work. The existing eight-MiB,
record, field and edit limits apply. Parsing and source preservation remain
independent of the runtime/editor geometry model.

The implementation uses only the maintainer's sequential-field tables and their
version/condition annotations. The PDF's purple conditional-field annotations
were inspected explicitly because plain text extraction loses that distinction.
Neither the reference library's implementation nor the specification's source-code
appendix supplied implementation text. Independent fixture code exercises every
version number, NPC/generator boundary conditions, distinct width/height values,
multiline strings, trailing classic-event fields, edits and malformed/truncated
input. These tests have not been run and the new reader has not been built yet.
Runtime mapping, source artwork and editor integration remain unfinished; reading
the source does not establish playable compatibility with all SMBX levels.

## Source geometry authoring model (implementation batch)

`SmbxGeometryEditor` projects existing top-level section frames, blocks, actor and
background anchors, player starts, warp endpoints and rectangular physics zones.
Positions stay in original global double-precision coordinates; a viewport must
subtract its double-precision camera before converting to drawing floats. Unknown
fields, source IDs, layer names, scripts and file references remain untouched.
Anchor markers intentionally carry no invented collision or sprite dimensions.
Circular zones and malformed/out-of-editor-range geometry remain in the source and
produce explicit issues, rather than being changed into rectangles.

Pointer movement changes one preview only, without parsing or modifying source.
Releasing a drag reparses one transaction of two coordinate fields (four edges for
a section frame). Snapping applies to movement deltas, preserving existing off-grid
positions. Block/rectangular-zone resizing changes W/H only. Cancel and no-op drags
retain the original document identity. Undo/redo retains at most 64 field-delta
transactions and four MiB of field/name text, rather than full document snapshots.
Revisions advance only for committed changes. Hit testing prioritizes objects over
section backdrops and accepts a camera-scaled radius for fixed-size anchor markers.

This editor view supports coordinates within ±2^40; larger source positions remain
preserved but are not editable in this view. Resize controls accept integral sizes
1–65535. These are explicit editor limits, not changes to the source grammar.
Moving a section frame changes its bounds only; a compound move-with-contents tool
is separate work. Record insertion/deletion, source picker/board integration,
artwork and runtime compatibility remain open.

The geometry mapping uses the same maintainer LVLX specification's SECTION,
STARTPOINT, BLOCK, BGO, NPC, DOORS and PHYSICS field tables, and the original legacy
reader's equivalent field names. No engine source or commercial layout was copied.
Independent regression source covers preview/commit, exact undo bytes, unknown
content/IDs, warp endpoints, section edges, cancellation, pointer allocations,
legacy fractional positions, malformed geometry and bounded history. These new
editor-model tests and this implementation batch are not yet built or run.
The earlier document parsers did compile during the rendering Release build gate;
their format-specific test suite still remains deferred.

## In-game source workshop (implementation batch)

The main level workshop now opens a dedicated SMBX workshop from its header. It
uses the host file picker for `.lvl` and `.lvlx`, caps reads at eight MiB, and builds
an editor before replacing the previous one. Saving writes the edited source bytes
through the host save picker with the original extension; it does not convert the
file to Suntrail or rewrite unknown fields. Back navigation retains the editor and
its history. Replacing an edited file requires the visible discard-and-open action;
saving a copy keeps the current draft open. Canceling a picker or a parse failure
leaves the previous source available. Revision-based dirty tracking is conservative:
undoing to a saved byte state can still show unsaved until another save.

`SmbxSourceBoard` records ordinary ProGPU drawing commands for visible rectangles
and fixed-screen-size source anchors. It has no per-object UI control tree or
per-object interop calls. Recording after an edit/pan/zoom is O(N + V) for N source
items and V visible objects, with bounded source storage and O(V) retained commands.
Stable UI uses the existing retained drawing contract. The camera rebases original
double-precision global coordinates before float projection. Section frames render
behind other objects; the selected outline uses a dynamic theme brush. This applies
the retained-display-list and visibility concepts already recorded in the engine's
cross-engine research; it adds no core rendering/shader/native ABI algorithm.

Mouse and touch use one captured pointer per drag. Other fingers cannot take over.
Pointer cancellation or capture loss cancels an uncommitted edit. Pan mode, zoom
buttons, fit and next-section controls navigate without source changes; arrow keys
pan and Escape cancels a drag. The selected item's kind, original ID, coordinates
and source row remain visible. Width/height actions apply only to supported source
rectangles. The board intentionally displays geometry rather than fabricated SMBX
art or silently substituted runtime behaviors.

This UI batch and its mouse/touch/source-round-trip regression source are not built
or run yet, following deferred validation. File-picker behavior, UI rendering and
all platform runs still need the final gate. New object insertion, record deletion,
palette drag/drop, actor artwork/config loading, complete physics-zone geometry,
more source properties and playable SMBX behavior remain open. The source workshop
is separate from the Suntrail play-test path until compatible runtime semantics and
assets are implemented.

## Object creation and deletion (implementation batch)

The source parser now retains insertion boundaries while reading each supported
object list. Legacy boundaries come from the actual versioned parser's consumed
separator tokens, never a text search for `"next"` that could match an NPC message.
LVLX boundaries identify top-level section terminators. A missing LVLX object
section is appended without rewriting existing sections. Deletion removes an
object record's source span and preserves surrounding delimiters/whitespace.

Structural undo uses guarded before/after text splices alongside existing field
edits. It reparses before publication, restores exact removed bytes, and restores
selection/record-index state in chronological undo order. History still has a
64-action/four-MiB text budget and never stores complete document snapshots.
Insertion/deletion and projection cost O(B + F), with no source parsing in drag
previews. Sections and fixed player slots cannot be deleted by the object tool;
deleting either warp endpoint removes its complete shared warp record. Remaining
events and references are preserved rather than rewritten automatically.

The palette creates block, background, NPC, rectangular water-zone and pipe-warp
records. New fields follow the documented default geometry and explicit empty /
false event/state values; they do not copy another object's custom data or unique
pointer. Legacy required fields follow the existing source parser's version gates.
NPC special-field presence is shared with the original legacy reader, including
version-sensitive IDs; unsupported pre-29 physics lists reject insertion. LVLX
uses named fields; legacy block height precedes width. Pipe entrance and exit
have separate direction enums: value 3 means enter down and emerge up, with DT=1.
This follows the same primary LVL/LVLX field tables already linked above.

The UI has a new blank LVLX canvas, an ID entry, palette drag/drop and tap stamping,
placement preview, selection mode and deletion. Escape cancels placement and returns
to selection. New/open replacement guards are independent. The blank canvas is an
original section/player scaffold; it contains no third-party layout or artwork.
Mouse/touch and structural regression source covers empty/missing sections, all 65
legacy versions, conditional NPC fields, separator-like text, source-byte restoration,
mixed history and complete warp deletion. This batch remains unbuilt/unrun under the
requested deferred-validation workflow. Actual artwork, object-specific runtime
behavior, section/player authoring, broader property editing and the other source
formats still remain unfinished.

## Pack artwork preparation (implementation batch)

`SmbxLevelPack` now opens a selected LVL/LVLX entry from the existing bounded
`AssetBundle`, discovers distinct block/BGO/NPC image requests, prepares CPU pixels
once per requested ID, and retains per-asset issues separately from the unchanged
source. It lists available source entries without selecting a different level
implicitly. The package API is implemented; ZIP selection and artwork rendering in
the workshop are not wired yet. It does not make SMBX gameplay compatible.

`SmbxArtworkCatalog` uses level-folder overrides before episode-folder assets.
Within each directory Suntrail explicitly prefers PNG, then GIF, then BMP; that
preference is not claimed as universal SMBX behavior. Lookup is case-insensitive
for portable packages, with case collisions rejected. Masks must accompany their
chosen color image in the same directory. Missing custom art reports the need for
a base configuration instead of substituting unrelated art. Malformed overrides
fail explicitly rather than falling back to an episode image. Preparation caches
success, absence and decode failures, supports at most 128 requested IDs, and caps
retained decoded pixels at 32 MiB. Image/mask decode and composition need temporary
memory in addition to that retained budget. No filesystem traversal, download,
script execution, per-frame decoding or GPU upload was added.

The reusable engine `RasterImage` owns straight RGBA8 bytes and consumes the existing
reviewed StbImageSharp package at the repository-pinned version. PNG/GIF/BMP headers
are checked before decode: at most 8192 per axis, 4,194,304 pixels and eight MiB
encoded bytes. GIF selects the first image. Binary black/white mask conversion is
supported; colored/gray masks and nonblack color pixels outside a legacy mask fail
because plain alpha cannot reproduce those raster operations. Transparent GIFs
without masks remain unsupported. PNG retains its alpha. These are explicit
compatibility limits, not claims about all historical image compositing behavior.

`SmbxNpcGraphics` reads bounded UTF-8/ASCII key/value configuration and keeps unknown
values for inspection, including image/script names without resolving or running
them. Missing frame metadata remains unresolved unless supplied through an explicit
base configuration. Known frame width/height/count and styles 0–2 allow bounded
editor frame selection from a vertical sheet; actual image dimensions must match.
Offsets remain typed metadata. Collision fields, timing modifiers and arbitrary
options are retained, not interpreted as a complete NPC simulation or animation
algorithm. Duplicate keys and invalid numeric layout values fail explicitly.

Primary research used the maintainer's historical
[custom graphics documentation](https://pgehelp.wohlsoft.ru/customizing/how_to_use_custom_graphics.html),
[NPC configuration contract](https://pgehelp.wohlsoft.ru/customnpcconf/npc_txt.html),
and [opacity-mask notes](https://pgehelp.wohlsoft.ru/customizing/opacity_masks_images.html).
These pages identify themselves as obsolete and target PGE 0.3.1.14 and older.
Their folder/naming and simple frame-layout contracts informed original code;
the linked replacement manual was inaccessible during research, so current
Moondust/TheXTech conventions are not asserted. No upstream implementation,
commercial sprite, base ID table or level layout was copied. Tests use original
tiny BMP/PNG pixels and independently authored source/config fixtures.

This is shared CPU asset preparation for Desktop, Browser and iOS; no managed or
native rendering algorithm, ABI or shader changes. Both renderer implementations
remain unaffected. New regression source covers override precedence, case/path
ambiguity, alpha/masks, bounded header rejection, cached failures, frame groups,
missing defaults and source preservation. This batch remains unbuilt and unrun
under the requested deferred-validation workflow. Actual packaged UI rendering,
base configuration support, real-file conformance and device memory/performance
measurements remain open.

## Package workshop integration (implementation batch)

The SMBX file picker now accepts bounded ZIP packages. A horizontally scrollable
package row lists LVL/LVLX entries by package-relative path and provides an explicit
open action. Staging a ZIP keeps the current editor available. Choosing a different
package checks unsaved drafts before replacing them; switching maps within the
active package retains their editor instances, selection and delta undo histories.
Package-wide dirty tracking includes maps other than the currently visible map.
Source and artwork preparation finish before a successful switch is published.
Malformed sources and preparation failures leave the old map available.

`SmbxPackageWorkshop` retains at most eight open draft editors against one immutable
bundle. This limits aggregate document/history retention; only the active map owns
prepared artwork, with transient overlap while preparing the next map. A saved
draft can be closed to free a slot. Save-copy writes only the selected original-
format level source. It does not rewrite the ZIP or copy its artwork: reopening a
closed draft loads the original package entry, as the UI explains. Saving an earlier
revision cannot hide newer edits. A future episode export must package edited
sources and assets together rather than imply that a standalone source copy does so.

The refresh-artwork action rebuilds preparation from the current edited source,
including inserted object IDs, while retaining its editor/history. Status reports
prepared pixel storage and the first unsupported/missing artwork issue. The board
still draws source geometry: sprite display, base object configuration and runtime
behavior remain unfinished. Decoding happens in explicit loading/refresh actions;
background scheduling and loading responsiveness still require the platform gate.

New original regression source covers retained multi-map history, dirty state in
inactive maps, exact edited bytes, failed activation, saved-revision tracking,
explicit closure, original-package immutability, draft limits and the in-game
package selector/replacement flow. The code and tests remain unbuilt/unrun under
deferred validation. No new shader, core renderer algorithm or native ABI was added.

## Selected artwork inspector (implementation batch)

The palette now contains a selected-artwork preview and an explicit frame-step
action. Block/BGO images and NPCs without complete frame metadata display the
whole source sheet with a label. NPCs whose supplied configuration completely
describes the simple vertical layout display a frame in the source direction.
Malformed layouts remain inspectable as a whole sheet with the error shown.
This does not invent block animation counts, NPC collision dimensions or runtime
frame algorithms. Preparation/refresh resolves images and metadata; rendering
records immutable commands only. The map itself still uses geometry markers.

GPU images belong to a compositor-owned public drawing extension and are rebuilt
from retained CPU pixels on a new device. The selected source rectangle is clamped
during filtering so neighboring frames cannot bleed into it. Details, public
projection/opacity API applicability, clean-room research and outstanding gates
are in `suntrail-engine.md`. This new UI/rendering batch and its regression source
are unbuilt/unrun, including the required shader-resource check. Full map sprite
placement, base configuration, playable compatibility and complete episode export
remain unfinished.

## Episode ZIP export (implementation batch)

The package toolbar now exports an episode ZIP containing every package file with
all currently open map sources replaced by their committed edited bytes. Inactive
drafts are included; uncommitted pointer previews are excluded. Original asset,
configuration, script and unopened-level bytes remain unchanged and retain their
package-relative names. Script files are transported as opaque data, never executed.
The archive is newly encoded: entry order is ordinal, timestamps are fixed to
January 1, 1980 and external attributes are zero. Original archive comments,
timestamps, attributes, compression bytes and explicit empty-directory entries
are not preserved. Deterministic compressed bytes are expected within one pinned
runtime/compressor, not asserted across different platforms or library versions.

Export first creates an immutable content revision and bounded ZIP bytes, then
uses the host save picker. Canceling or failing to write does not change the
package baseline or saved revisions. Only after a successful write does that
snapshot become the new episode baseline. Closing and reopening a saved draft
then reads the latest successfully exported source; later exports include its
changes even while it is closed. Before the first episode export, the input ZIP
remains the baseline. Standalone source-copy saves still do not replace that
episode baseline. This supersedes the earlier original-input-only reopening rule.

Snapshots retain exact draft revisions. Later edits stay dirty when an earlier
snapshot finishes saving. Sequence checks prevent an older completion from
overwriting a newer completed export. Closing/reopening a draft produces a new
editor identity, which prevents an old completion from marking that new editor
saved. The toolbar disables editing and other file actions during its save flow.

Reusable `AssetBundle.WithReplacements` shares unchanged owned file bytes and
copies replacements after validating the complete final expanded size. `WriteZip`
uses the BCL ZIP writer behind an original bounded output stream. Both retain
existing 128-entry, eight-MiB-file, 32-MiB-expanded and 16-MiB-archive limits.
Missing, duplicate or escaping replacement paths fail explicitly; no files are
silently omitted to fit the output budget. The limit is checked during compressed
writes, including central-directory finalization. A larger edited package can
remain open even when it cannot fit the current export limit.

Implementation consulted Microsoft's primary
[ZIP entry creation contract](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.ziparchive.createentry?view=net-10.0)
and [entry timestamp contract](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.ziparchiveentry.lastwritetime?view=net-10.0).
No ZIP encoder/compressor implementation was copied. Complexity is O(U + E log E)
for expanded bytes U and entries E, within fixed package bounds; unchanged assets
are shared across content revisions. ZIP output storage and its owned returned
copy are each bounded by 16 MiB, in addition to source revisions/compressor scratch.
No rendering, native ABI, shader or platform-specific file API was changed.

Original regression source covers binary/non-ASCII files, deterministic metadata,
source replacement ownership, invalid names, compressed-output limits, multi-map
edits, inactive/closed draft preservation, preview exclusion, asynchronous revision
tracking, stale completion and canceled save state. This code, its tests and host
save-picker flows remain unbuilt/unrun under deferred validation. The artwork
inspector, map sprite placement, runtime compatibility and all broader outstanding
goal requirements still need their final gates.

## Split object configuration metadata (implementation batch)

`SmbxConfigDocument` preserves the original UTF-8/ASCII INI bytes and reads named
sections, key/value fields, quoted strings and unquoted semicolon comments.
Unknown field values stay raw. Typed accessors provide finite invariant numbers,
integers and boolean flags, without executing Qt variants, expressions, includes
or scripts. Duplicate names fail explicitly. Bounds are 256 KiB, 4096 lines/fields
and 128 sections per file; parsing/storage is O(B). This is the documented simple
configuration subset, not a complete QSettings serialization implementation.

`SmbxConfigPack` reads caller-supplied split indices from `lvl_blocks.ini`,
`lvl_bgo.ini` and `lvl_npc.ini`, resolves their `config-dir` inside the package,
and loads requested block/background/NPC definition records by ID. Complete source
fields remain available. Graphics/physical dimensions have typed bounded access;
collision codes, shape codes, animation fields and algorithm identifiers remain
uninterpreted metadata. In particular, INI frame-count/style semantics are not
assumed interchangeable with NPC.txt's per-direction groups. The reader imports
no default ID table, engine implementation or commercial artwork.

The workshop's Load definitions action accepts a ZIP with exactly one `main.ini`
and its split files. It keeps prior definitions on load failure and displays the
selected object's supplied name and physical/graphical dimensions. It does not
change level bytes, hit testing, physics or sprite placement. Definition loading
retains the existing 128-entry/32-MiB expanded package limits; complete large
configuration archives need a later demand-loaded archive representation.
Monolithic indices, application-relative roots and unsupported string encodings
fail explicitly rather than selecting an ambient application directory. Loaded
definitions are separate from the episode export; base artwork resolution and
behavior compilation still remain open.

Research found the maintained
[custom graphics guide](https://github.com/WohlSoft/Moondust-Devkit-Help/blob/master/Customizing/CustomGraphics.md)
and [NPC configuration guide](https://github.com/WohlSoft/Moondust-Devkit-Help/blob/master/EditNPCConfiguration/About.md)
in the maintainer's official documentation repository, resolving the earlier
documentation-site access limitation. The current guide uses `background2-*`
for section images. Lookup now prefers that name, retaining the older documented
`background-2-*` alias within the same level/episode precedence.

The public configuration data contracts were inspected in the maintainer's
[main index](https://github.com/WohlSoft/Moondust-Default/blob/master/main.ini),
[NPC index](https://github.com/WohlSoft/Moondust-Default/blob/master/lvl_npc.ini),
[block index](https://github.com/WohlSoft/Moondust-Default/blob/master/lvl_blocks.ini),
[BGO index](https://github.com/WohlSoft/Moondust-Default/blob/master/lvl_bgo.ini),
and individual [NPC](https://github.com/WohlSoft/Moondust-Default/blob/master/items/npc/npc-1.ini),
[block](https://github.com/WohlSoft/Moondust-Default/blob/master/items/blocks/block-1.ini)
and [BGO](https://github.com/WohlSoft/Moondust-Default/blob/master/items/bgo/background-1.ini)
configuration examples. Only field/path contracts informed original code; their
names, artwork, object data sets, behavior implementations and field-value tables
were not imported. Independent test definitions use different original IDs,
dimensions and opaque future-rule values.

Regression source covers exact bytes, comments/quotes, unknown data, duplicate and
malformed syntax, split paths, absent definitions, typed dimension errors, root
escapes and section-background filename precedence. This batch remains unbuilt
and unrun, including UI/file-picker checks. All core managed/native rendering and
shader algorithms are unchanged; this is shared sample-side configuration work.

## Indexed definition archives (implementation batch)

The definition picker now uses `IndexedAssetArchive`, an `IAssetSource` that owns
compressed ZIP bytes and a central-directory index. It supports 128 MiB encoded,
16,384 entries, eight MiB per file and one GiB declared aggregate expanded data.
These limits describe the input, not a one-GiB allocation: only a requested entry
is decompressed into owned output bytes. Unrequested entries are not expanded or
CRC-checked until read. Index creation validates structure, normalized relative
paths, duplicates, symbolic links and entry bounds before exposing the source.
Stored/deflated single-disk ZIPs are supported; ZIP64 remains unsupported.

`ReadAsync` reads a seekable input into one owned buffer, respects cancellation,
and leaves the caller's stream open. In-memory `Open` copies its caller's bytes.
Reads serialize access to the BCL archive's shared input stream and verify exact
expanded length and CRC. The archive retains no expanded-byte cache. Disposal
releases compressed/index references while previously returned byte buffers
remain usable. Metadata listing does not cause decompression. Indexing costs
O(C + E log E), reads O(U), with compressed bytes C, entries E and requested bytes U.

`SmbxConfigPack` accepts the shared asset-source interface, with explicit borrowed
or owned lifetime. The picker owns its archive, disposes failed/stale replacements,
and releases the previous owned archive after a successful replacement. Parsed
definitions use bounded FIFO retention: at most 4096 entries, 16 MiB source bytes
and 65,536 fields, with additional bounded parsed metadata. This supersedes the
previous hard limit on total lifetime lookups. A 64-entry failure cache avoids
repeated inflation of the same invalid configuration on selection. The small
episode editing/export bundle retains its earlier limits; the larger indexed
source is currently wired to definitions only.

The structural and CRC checks were refactored directly from the original
in-repository `ProGPU.GameEngine/Assets/AssetBundle.cs` implementation. The BCL
remains responsible for ZIP/decompression; no foreign implementation was copied.
Original regression source compares eager/indexed file bytes and covers packs
beyond the eager limit, requested-file counters, deferred corruption detection,
ownership/disposal, cancellation, normalized paths, entry limits and metadata
cache eviction. These tests and builds remain deferred, so no measured memory or
loading-speed claim is made. The phone was checked again and remains unavailable.

## Base artwork resolution (implementation batch)

The loaded definition pack now supplies fallback images to package artwork
preparation and the selected-object inspector. Resolution checks the level's
custom folder, then its episode folder, then the selected base definition's
`image` under the pack's `graphics-level` root. NPC.txt overrides continue to
come from the level/episode even when the image comes from the base pack. The
INI animation model is not silently converted into NPC.txt frame semantics.
Unknown layouts still show the whole sheet in the inspector.

Explicit image paths are relative to `graphics-level`; absolute paths, URLs and
references escaping that root are rejected. Suntrail's adapter first checks the
exact relative path. For a bare filename only, it also accepts a unique matching
descendant under that root, accommodating object subfolders. Duplicate descendant
basenames are an error unless an exact path resolves them. This unique-name rule
is Suntrail's explicit import policy, not a claim that every SMBX-family engine
uses that lookup algorithm. Case-insensitive package indexing rejects ambiguous
case variants and preserves original paths in diagnostics. No ambient assets are
searched or downloaded.

PNG/GIF/BMP images use the existing bounded CPU decoder and legacy mask rules.
A mask is resolved in the selected image's directory and archive only. Invalid
custom images do not silently fall back to the base image. Prepared images own
their pixels and stay usable after a definition archive is disposed. Loading a
replacement definition pack prepares current artwork before replacing the old
source; source bytes, revision, selection and undo history are unchanged. Later
map openings and explicit artwork refreshes use the currently selected pack.
Base assets are borrowed during preparation and are not copied into episode ZIP
exports. Indexed archives only expand requested definition/image/mask files.

The path/name index costs O(E) time and storage at load for up to 16,384 entries;
lookup is expected O(1), with decoded pixels and definitions subject to existing
budgets. This is shared sample CPU preparation; managed/native renderer algorithms,
the C ABI, shader code and frame submission are unchanged by this batch.

Primary contract references consulted:

- [Moondust custom graphics documentation](https://github.com/WohlSoft/Moondust-Devkit-Help/blob/master/Customizing/CustomGraphics.md): level/episode precedence, image and mask naming, and distinct frame models.
- [Public main.ini example](https://github.com/WohlSoft/Moondust-Default/blob/master/main.ini): the graphics-level root and application-relative flag.
- [Public NPC definition example](https://github.com/WohlSoft/Moondust-Default/blob/master/items/npc/npc-1.ini): an image filename in a split definition. No default IDs, artwork, values or engine implementation were copied.

Original regression source covers custom/base precedence, paired-mask isolation,
case handling, ambiguous versus explicit paths, invalid/missing images, root
boundaries, prepared-image lifetime, and refresh preserving source/undo/export
separation. This batch is unbuilt and untested. Board sprite placement, complete
frame/behavior interpretation and compatible runtime gameplay remain open.

## Prepared sprites on the source board (implementation batch)

The board now records visible prepared images through the retained artwork batch
extension, with source geometry overlays kept available for editing. Its CPU
`SmbxBoardArtwork` model resolves layout after immutable source or artwork changes,
not during drawing. The level pack retains definitions for successfully prepared
images (at most 128), so later board layout does not touch the archive. A malformed
definition is reported without discarding an otherwise valid decoded image.

Supported placements currently include explicitly static and simple vertical-sheet
block/BGO images and NPC.txt sheets whose supported style, frame dimensions and
count are complete. The board shows the first frame; NPC direction selects the
corresponding group. NPC body dimensions must come from NPC.txt or the supplied
definition. The editor preview attaches the graphic to that body's bottom center
and applies graphical offsets. These are preview placements, not a claim of
complete engine-specific animation algorithms, state handling or collision rules.
INI frame-style/count semantics are still not converted into NPC.txt semantics.

Unknown layouts remain source markers with layout issues. Sizable nine-part block
tiling, resized blocks without a sizing rule, and complete layer/z-order behavior
remain open. Ordinary block frames must match the source rectangle size. Current
sprite order follows source record order; editing overlays are drawn above sprites.
This is explicit editor behavior, not a substitution for compatible runtime
layer ordering. The package status reports placed-sprite and unresolved-layout
counts after opening or refreshing artwork.

Draw recording culls sprite bounds after double-precision camera rebasing, then
copies only visible sprite records into an immutable batch. Stable retained replay
does not re-record. Selection checks sprite bounds as well as the original source
anchors and rectangles, in matching reverse record order. Drag preview obtains the
current source anchor from the editor's O(1) preview state, so it reuses layout and
decoded images; only committing source changes rebuilds the placement model.
None of these operations replace source collision sizes with image dimensions.
New IDs whose artwork was not prepared still require the explicit artwork refresh.

Layout preparation is O(N) time/storage for N bounded geometry records; changed
recording and hit testing are O(N), with O(V) retained sprite commands for V visible
items. No image reads, source decoding, definition lookups or GPU calls occur in
layout construction or pointer motion. Drawing consumes the canonical batch shader
introduced in the prior batch; no core managed/native renderer or C ABI changes
are made here. The cross-engine preparation/retention research in the engine notes
continues to apply. Public format contracts are the previously linked Moondust
custom graphics and NPC.txt documentation; no foreign source or assets were used.

Original regression source covers static/vertical/directional frame rectangles,
placement offsets, unresolved and resized images, archive-independent prepared
metadata, sprite picking, unchanged model identity during drag, cancel, commit,
undo and editor detachment. This batch is unbuilt and untested; visual, touch,
shader, performance and platform verification remain deferred to the final gate.

## Static sizable blocks (implementation batch)

The board now accepts supplied static 96×96 sizable-block images as nine 32×32
patches. One retained sprite represents the entire block: corners keep their size,
edge strips repeat along their long axis, and the center repeats in both axes.
Partial final tiles stop at the far border. The GPU remaps coordinates with fixed
work per covered fragment rather than expanding one CPU command per tile. Sprite
selection and dragging continue to use the source rectangle and prepared metadata.

Current supported destination dimensions are 64–65,536 source pixels per axis.
The lower limit preserves two complete borders; the upper limit bounds f32 local
coordinate precision. Smaller, larger, animated or differently sized source sheets
remain editable source rectangles with a layout issue. These are explicit current
adapter limits, not a claim about all sizes allowed by the source file format.
Full semantic layer/z-order handling remains unfinished.

The [public custom-graphics documentation](https://github.com/WohlSoft/Moondust-Devkit-Help/blob/master/Customizing/CustomGraphics.md)
provides the nine-part layout and 96×96 SMBX sheet contract. The periodic sampling
implementation is original ProGPU shader code, extending this branch's existing
prepared-art batch; no foreign shader, tile table, artwork or engine source was
copied. Regression source now covers one retained sizable placement, dimensions
and unsupported boundaries, plus independent pixel references for corners, edges,
center repetitions and partial tiles. All builds/tests remain deferred.

## Source-layer visibility controls (implementation batch)

The editor palette now lists declared LAYERS/LR groups and groups referenced by
object LR fields, including object counts and empty declared groups. Users can
hide/show the selected group or show all groups. Missing LR membership has a
separate unassigned group; an explicit empty name and differently cased names
remain distinct. This is an ordinal source-name view, not a claim about every
engine's runtime name matching or default-layer normalization.

These controls change editor visibility only. All groups initially show, including
ones whose saved HD flag is set; HD/LC flags and event behavior are preserved and
not interpreted by this view. Toggling visibility does not alter source bytes,
revision, export or undo history. Source commits and undo rebuild membership while
preserving view choices for surviving names. Opening another editor starts fresh.
Malformed layer fields remain preserved and are recorded in model issues.

Hidden groups are excluded from sprite recording, geometry overlays and hit tests.
Hiding a group cancels an in-progress drag and clears a now-hidden selection.
Membership is prepared once per immutable source change in O(R + G + L) time and
storage, with records R, geometry items G and groups L; indexed visibility checks
are O(1). Pointer preview keeps the prepared model. The board invalidates its
retained visual whenever visibility changes; no renderer/shader/core C ABI change
is required. Layer visibility does not implement sprite z-order or event actions.

Source contracts were checked against the LAYERS/LR and object LR tables in the
previously cited LVLX specification and the original repository's legacy parser.
The LVLX table labels HD/LC as strings while legacy stores Boolean flags, another
reason this editor-only control does not silently apply them as runtime semantics.
Original regression source covers named/empty/unassigned groups, case distinction,
hidden picking, drag cancellation, commit/undo visibility retention and unchanged
source/revision. Builds, tests and UI/performance validation remain deferred.

## Object layer and NPC direction properties (implementation batch)

The palette now exposes a selected object's layer name and NPC left/random/right
direction actions. Layer assignment writes that object's LR reference; it does not
rename other objects or automatically create a runtime layer declaration. Referenced
names appear in the editor visibility list. Random writes the documented D:0 value;
the first-frame editor preview remains deterministic and does not simulate random
NPC initialization.

Existing field edits preserve surrounding source bytes through the bounded field
history. Missing LR/D fields in LVLX use a guarded source splice after the final
value, preserving the original trailing separator, whitespace, BOM and line endings.
Undo removes the inserted field exactly; redo restores it with selection intact.
Legacy versions may update existing property slots only. No unsupported sequential
field is inserted into an older legacy version. Property changes reparse/project
before publication, with the existing history budget and no script execution.
Layer-name editing is bounded to 1024 characters and uses format-specific encoding.

Keyboard routing now lets focused text fields handle their keys instead of invoking
map pan/delete shortcuts. This applies to the workshop's layer and ID fields. The
property text refreshes on source/selection changes rather than each pointer preview,
so an unrelated UI refresh does not replace a user's uncommitted typed layer name.

These properties use the previously cited LVLX LR and NPC D (-1/0/+1) contracts and
original ProGPU source encoding/splice infrastructure. Rendering algorithms, native
ABI and shaders are unchanged. Regression source covers field insertion with and
without trailing separators, string escaping, unknown fields, byte-exact undo/redo,
semantic no-ops, bounds and legacy slot behavior. Tests, keyboard interaction checks
and platform builds remain deferred; full properties/runtime compatibility is open.

## Layer declarations, rename transactions and warp settings (coding batch)

Layer controls now create declarations and rename named groups. Legacy insertion
uses the parser-owned layer-list separator only in versions that contain that
list; LVLX appends to an existing LAYERS section or creates one. Renaming updates
declarations, object LR references, NPC attached-layer LA references and documented
classic-event references together: ML, legacy LH/LS/LT slots, LVLX LH/LS/LT string
arrays and MLA encoded moving-layer entries' LN fields. Changed array elements
retain surrounding separators/whitespace; unrelated fields remain intact.
Destination-name collisions and the existing transaction/history limits reject
before publication. One undo restores the whole change.

Script text, expressions and unsupported custom/modern event actions are not
rewritten; the UI states that limitation after rename. This is not a general script
refactoring tool or proof of complete runtime layer semantics. Missing/default
membership normalization remains outside the currently explicit source-name model.

Selected pipe/door endpoints now expose warp type, distinct entrance/exit direction
enums, target filename and warp ID, level-entrance/exit flags and two-way behavior.
Both source endpoints share the same settings record. Target ID zero is the
documented default start point. A multi-property transaction preserves existing
unknown data and source text; missing LVLX fields are inserted through a bounded
whole-record splice, while sequential legacy versions update existing slots only.
Absent legacy fields whose requested value is the default are left absent. Portal
type is currently offered for LVLX only. The filename is stored as source data;
this operation performs no target-file access or script execution.

Implementation follows the previously cited LVLX DOORS, LAYERS and EVENTS_CLASSIC
tables, including the MLA/LN table, and original ProGPU parser/encoding/history
code. No external engine implementation was copied. These CPU editor changes do
not modify core renderer algorithms, shaders or native contracts. Per the latest
instruction, this coding batch added no QA runs, commits or pushes; source/property,
UI, legacy and transaction regression work joins the final QA phase.

### Geometry playtest (implementation pending QA)

The SMBX workshop now has a Geometry playtest action. Select a player anchor to
start there, or use player 1/the first available start. A 30 × 48 Suntrail courier
must fit inside a section and clear of blocks. All source block rectangles become
solid for this preview. Source artwork is retained, including base-definition art
for standalone files. USE enters supported local pipes/doors; instant/portal
transfers activate on contact. Restart restores the start; Edit restores the same
draft and undo history. Controls offers floating/fixed stick or arrows, sprint and
button-size choices shared with the main game.

This preview does not emulate original NPCs, scripts/events, layer behavior,
physics zones, original block effects or cross-file/world-map exits. It uses
Suntrail movement and art, not Mario character calibration. The Controls panel
includes a bounded limitations report. Full SMBX compatibility remains unfinished.
