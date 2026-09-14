# Invocation-private GPU query traversal

Acceptance: **ProGPU.Wpf.ShowcaseApp**, pointer selection and geometry-region
queries against the presented native MIL owner index. The blocking path is FXC
compilation of ProGPU's resumable traversal before the first native owner query.

## Cause and change

The canonical shader at `ab32ad25` carries a 64-element array in `QueryTraversal`
and passes that aggregate through an inout iterator. A diagnostic wrapper of the
already pinned Naga `0.19.2` at wgpu commit
`87576b72b37c6b78b41104eb25fc31893af94092` exports its HLSL. With shader model
5.1 and strictness, Windows SDK FXC reproduces X3511 in the dynamic child-stack
push loop and its enclosing traversal/caller loops. The wrapper uses the existing
locked dependency source unchanged; its default diagnostic resource registers are
not claimed to duplicate the product pipeline layout.

Moving only that array to `var<private>` lets the same point, bounds and ellipse
entrypoints compile. Each invocation owns its storage and initializes exactly one
traversal. The scalar iterator state remains inout. This is neither workgroup
storage nor a CPU fallback, and requires no barrier, binding, allocation, dispatch
or host change. There is no interleaved second traversal in any entrypoint.

The original ProGPU shader at `ab32ad25` is implementation provenance. No external
algorithm or implementation is imported. Geometry, clipping, stack capacity,
node/local-reference/LIFO order, result insertion and counters remain unchanged.
Storage remains O(64) per invocation, with the same traversal complexity.
The managed and C++ providers both consume the one canonical WGSL file.

## Validation — 2026-09-14

- Original generated point HLSL fails X3511; invocation-private point, bounds and
  ellipse HLSL compile with the same FXC executable and flags. Existing generated
  HLSL X4000 warnings remain visible, not suppressed or reported as clean output.
- Dense Metal: 120 full result records and 100 public ordered queries match the
  independent original/pre-refactor reference. Sparse Dawn Metal: 168 full records
  and 140 public ordered queries match the independent original-shader reference.
  Every counter/unused slot remains compared. The current sparse legacy/staged
  Metal differential also passes 168 records.
- Both native providers rebuild with AppleClang and Windows ARM64 MSVC. All 19
  native CTests and 54 original source-diagnostic tests pass. The source test
  guards invocation-private ownership and stack indexing; the GPU differentials,
  not string checks, establish retained query behavior.
- The rebuilt full native consumer passes on Metal with automatic single-pass
  queries and on system ARM64 WARP with explicit DXC/ordered stages. Both retain
  38 resources/11 draws/174080 coverage and original owner/generation, 16 repeated
  waits, participation and region-first checks. DXC stdout SHA-256:
  `3151874456b24e93b293c9b723887fafd21769d187315c471a731ee1f3d6f94f`.

The stock Silk ARM64 dependency with automatic FXC/single-pass now completes its
first point readback and all 16 repeated waits, but exits `0xC0000005` after the
first bounds submission. The submission call took 55.8 seconds; no bounds readback
was published. This is a terminal runtime failure, not a passing default lane or
a reason to increase a timeout.

The stock FXC **ordered-stage** consumer passes the complete same native fixture
on system WARP, including bounds and ellipse queries. Stdout SHA-256:
`b649245074fa771e98f4587bb3c80ca61fae8bb0344e13a38027da5ea1ed565e`.
No DXC selection or compiler-runtime replacement was used for this comparison.
Build CI now also runs its complete ordered-query package step on Windows x64
and ARM64 with system software adapters, retaining the separate default and
NativeAOT steps and their original deadlines. This adds qualification coverage;
it does not waive the failing single-pass default or qualify a default switch.

The independent stock-FXC Windows GPU contract passes both dense (120 full
records/100 public queries) and multi-level sparse (168/140) fixtures against
the original external reference buffers. No CPU geometry is used in dispatch.
Their stdout SHA-256 values are respectively
`6c050825fd0e9a4eccbaa33201d4cebe8f80ffae246b6f7f0fbad62e5da671e6`
and `d48cb31aa7dcb3afce13f86a0105df9c0d0e8015820d081d66c3207279174659`.
The existing 54 source checks and workflow lint still pass after adding Windows
coverage; final-head hosted execution is not inferred from these local results.

Windows diagnostic renderer hashes:

- `progpu_native.dll`:
  `29b2c14fe370556dd30d7bcbe07963c8f5c2ff42d3f436bdfd61b92d85003012`
- `progpu_native_dawn.dll`:
  `966103451f83a613443cd72dc1d9786d7dd015b06d02a794c3f4136fae329c8f`

Artifacts: `artifacts/query-hlsl`, including generated original/candidate HLSL,
DXBC, pinned diagnostic Cargo inputs, scripts and Windows runtime logs. Native
runtime checks use explicit rebuilt-DLL overlays, not a newly CI-qualified NuGet.
No VM settings, system DLLs, upstream source, runtime defaults or dependency pins
change. Final Windows runtime/package, hardware and actual Showcase gates remain
required; successful compiler repair does not prove GPU execution or a merge.
