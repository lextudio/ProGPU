# Native scene-builder append capacity

## Bounded implementation

Acceptance: **ProGPU.Wpf.ShowcaseApp**, live input/resize/themes/popups/wheel/capture
followed by 120 warmed native presentations. Time Profiler identified repeated
C++ command/resource-vector reallocation during scene compilation. This change
improves capacity growth, not scene caching, invalidation, rasterization or input.

`scene_builder_detail::reserve_append` preserves allocation preflights before
publishing related records. It checks addition against the allocator's `max_size`,
reserves at least the requirement, otherwise doubles capacity from eight entries,
and saturates without overflow. Existing capacity requires no allocation. All 45
incremental preflights for commands, resources, brushes, gradient stops and text
styles use it. Explicit caller reserve hints, exactly sized payload/serialization
arrays and already geometric sparse metadata remain unchanged. Reset retains
capacity. Protocol limits and exception/error behavior remain authoritative.

Repeated single-entry appends change from potentially O(N²) moves to O(N) total
moves and O(log N) allocations, retaining O(N) storage. A growth operation remains
O(N); bulk insertion preflights once. Requested spare capacity is below twice the
requirement except for the eight-entry initial allocation. Pointer-owning record
moves and allocator preflight are not numerical SIMD work. No shader, fallback,
quality, texture-retention or public ABI changes are involved.

## Managed/native applicability

Native MIL and semantic API consumers share the same C++ builder. There is no
WPF-local workaround. The managed picture compiler's accumulators in
`src/ProGPU.Scene.Native/GpuPictureNativeSceneCompiler*.cs` use `List<T>.Add` and
`AddRange`, not exact capacity assignments on each append; their existing growth
is amortized. No shared scene semantics or wire records change. Managed source
export allocations remain separate and are not fixed by this native helper.

## Tests

The allocator-controlled fixture checks 4,097 appends with at most 11 allocations,
retained values, zero append, bulk append without another allocation, injected
allocation failure, `size_t` overflow and a non-power-of-two maximum of 31.
A real 3,072-command save/draw/restore fixture compares complete serialized bytes
for incremental, preallocated and reset/reused builders, including unchanged
streams after rejected input. Both wgpu-native/Dawn libraries compile, all 19
CTest suites and 125 native/managed interop tests pass, generated native
contracts/coverage and actual dylib exports pass. Capacity is not serialized identity.

## Profiling record — 2026-09-14

Artifacts: `/Volumes/1TB-macOS/progpu-native-release.KYesd6`, including matched
before/after Time Profiler, Allocations and Metal System Trace captures, exported
CPU/Metal tables, EventPipe allocation traces and analysis helpers. This uses an
explicit source/package overlay on macOS ARM64 M3 Pro, not final indexed packages.
Showcase is Release/DLL launch with `UseAppHost=false`; an initial no-restore build
without that property failed on the missing apphost. The corrected build has no
warnings/errors. Restore current PresentationFramework/bridge/backend/native
outputs after building the diagnostic graph, which otherwise recopies old packages.

The Instruments cohort changes only the native dylibs; its Showcase hash is
`f41319fed58d7c05322c3399274d35b65223f9725d0a94fd1a735dd4ed163ea5`.
Native wgpu dylib before:
`ab035f5e14aaab0c2a74fc9621d72216e7b1f23a190d43de86c9ec46d4911eb7`;
after: `e76956d7db8c2a8d97f341b9c96ef0aa67f3ffb0e351fa1120e532b7f81e3e9b`.

Time Profiler records 14,291 before/11,322 after one-millisecond samples across
startup, input and warmed rendering. Command-vector reserve self time changes
from 1,463 to 4 ms, resource-vector relocation from 930 to 4 ms, and total native
library self time from 3,564 to 1,243 ms. These identify the removed hotspot;
startup JIT/scheduling remain included, so they are not frame-latency percentiles.
The earlier uninstrumented Release baseline passes input plus 120 frames, with
compile p50/p95/p99 17.730/18.297/18.429 ms and host CPU
23.035/26.004/47.196 ms, allocating 4,144,731.53 managed bytes/frame.

Before-change EventPipe allocation ticks attribute approximately 430 MB to byte
arrays and 154 MB to `PortableVisualState` over the whole startup/input/measured
run. These are sampled weights, not exact per-type allocation totals. The native
capacity change does not remove this managed export work.

The follow-up EventPipe run remains dominated by the same types (approximately
454 MB byte-array and 165 MB `PortableVisualState` allocation-tick weights).
Its reporter additionally prints measurements on memory failure; those full-run
sampled totals are diagnostic attribution, not matched per-type reductions.

Some before and after runs, including an uninstrumented candidate, still fail
the original 1 MiB endpoint memory-growth gate. Inventory reaches 63 deferred
raster batches and 138,364,752 known-owned GPU bytes; six textures remain at
8,410,072 logical bytes while retained buffers vary. Baseline Metal
`currentAllocatedSize` peaks at 183,418,880 and ends at 133,349,376 bytes; that
device/driver metric differs from logical ownership. Periodic latest-submission
poll/drain behavior is unchanged. Timing-dependent pending-batch endpoints do
not prove an unbounded leak or a memory improvement. No growth limit, completion
contract, source action or deadline was relaxed.

Final stable retention, paired unprofiled measurements, exact package/platform
qualification and CI remain open. Separately, Build `34803203731` passes explicit
ordered Windows package consumers and full-capacity differentials, but default
x64 returns an ellipse summary hit with zero list records and ARM64 exits 127
after a bounds submission. CPU capacity does not fix that FXC/runtime problem.
No pins or merges advance on this checkpoint.

### Paired uninstrumented runs

The report now prints timings before throwing on a failed memory gate, without
changing that gate. Both before/after runs below use that same rebuilt Release
Showcase hash `99036df722fead184d2cb8b956284c5381dfb734abc882c5f7f57ac41fc9ea88`,
the same bridge/backend/source assemblies, and the native dylib hashes above.
Each runs full live input and measures 120 genuine frames with 2,292 commands
and 118 draws. Log files are `baseline-report-{1,2}.txt` and
`capacity-report-{1,2}.txt` in the artifact directory.

| Metric | Before 1 / 2 | After 1 / 2 |
| --- | --- | --- |
| Compile p50/p95/p99, ms | 18.163/18.746/19.227; 17.767/18.141/18.614 | 3.673/4.036/4.307; 3.684/4.021/4.200 |
| Host CPU p50/p95/p99, ms | 23.742/26.346/48.059; 22.971/25.751/44.280 | 20.282/29.958/32.773; 21.162/29.378/47.361 |
| Process CPU, ms | 3,585.468 / 3,390.930 | 1,658.185 / 1,666.218 |
| Wall duration, ms | 3,241.242 / 3,136.366 | 2,648.666 / 2,675.919 |
| Managed allocation, bytes/frame | 4,124,893.27 / 4,141,582.73 | 4,145,687.67 / 4,137,308.93 |
| Native-owned bytes, start → end | 94,950,960 → 99,437,248; 98,669,552 → 99,437,248 | 13,386,976 → 128,120,672; 87,992,368 → 119,925,408 |
| Memory gate | fail / pass | fail / fail |

The compiler improvement repeats; it is not an overall latency or qualification
pass. Surface-acquire p50 rises from 0.025/0.021 to 11.064/11.519 ms as compile
time falls, consistent with changed presentation pacing. Host p95 is worse in
these samples and p99 is variable. Managed allocations do not materially improve.
Candidate Metal `currentAllocatedSize` peaks at 215,711,744 and ends at
162,791,424 bytes: no memory-reduction claim is justified. Native pending-batch
retirement and paced end-to-end latency still need qualification. Do not hide
the failed endpoints, exclude acquire time or select only the passing baseline.

## Research and decisions

Primary sources rechecked before implementation:

- [C++ vector capacity](https://eel.is/c++draft/vector.capacity): linear growth
  work and allocation-before-insertion guarantees motivate geometric preflights.
- [Skia recorder](https://api.skia.org/classSkPictureRecorder.html) and
  [SkParagraph](https://github.com/google/skia/blob/main/modules/skparagraph/include/Paragraph.h):
  retain recorded ownership and reusable text layout; no shaping/atlas change.
- [Direct2D performance](https://learn.microsoft.com/en-us/windows/win32/direct2d/improving-direct2d-performance),
  [DirectWrite input](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextlayout-hittestpoint),
  [Win2D device](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_CanvasDevice.htm):
  preserve batching/resource ownership and layout-derived input, without flushes.
- [WebRender](https://firefox-source-docs.mozilla.org/gfx/RenderingOverview.html)
  and [Vello Scene](https://docs.rs/vello/latest/vello/struct.Scene.html): improve
  the existing scene producer without changing culling, GPU execution or caches.
- [Parley](https://docs.rs/parley/latest/parley/struct.Layout.html) and
  [HarfBuzz plans](https://harfbuzz.github.io/harfbuzz-hb-shape-plan.html): layout and
  shaping reuse are independent; this profile does not justify changing them.

Only architecture/API contracts were consulted. The helper and fixtures are
original ProGPU code; no foreign implementation or dependency was copied.
