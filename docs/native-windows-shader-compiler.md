# Windows shader compiler dependency

## Acceptance path and scope

`ProGPU.Wpf.ShowcaseApp` startup and first source input are blocked by slow
FXC compilation and a separate installed-WARP execution failure. The matched
[query compiler/runtime investigation](native-hit-query-segment-lanes.md)
shows that the exact-ABI DXC-capable dependency executes the complete owner
fixture with development WARP, with family submissions around one second.
System WARP still crashes with either compiler. Neither a compiler switch nor
the testing-only WARP package qualifies the application or permits a merge.

`eng/build-wgpu-native-windows.ps1` builds that optional dependency from pinned
inputs. `WgpuDx12CompilerOptions` now connects explicit selection to the shared
product device factory. This does not change automatic selection, the stock
Silk package payload, the renderer, or existing application/CI gates.

## Reviewed dependency inputs

- The binding ABI and native/header revisions remain in
  `eng/progpu-native-wgpu.version.json` (Silk.NET 2.23.0, native `33133da4`).
- `eng/wgpu-dxc/build-inputs.json` pins wgpu `87576b72`, Rust/Cargo 1.98.1,
  the optional `dxc_shader_compiler` feature, and the dependency lock hash.
- `eng/wgpu-dxc/Cargo.lock` is the preserved dependency resolution from the
  isolated Windows DXC experiment; builds use `--locked`. It is generated
  package/version/checksum metadata, not copied renderer implementation.
- The small original `dependency.toml` overlay enables the optional feature
  on that same wgpu-hal package. It is applied only to an isolated external
  dependency checkout. No Rust implementation or public header is patched.

Cargo's documented [dependency feature unification](https://doc.rust-lang.org/cargo/reference/features.html#feature-unification)
allows the additional direct dependency to enable an existing transitive
feature without creating another HAL implementation. The script checks the
resolved metadata for exactly one pinned HAL with that feature. The
[locked build contract](https://doc.rust-lang.org/cargo/commands/cargo-build.html#manifest-options)
rejects dependency drift. Source revision and compiler capability are separate:
the stock binary has the matching ABI but lacks this feature.

## Build

From a Windows PowerShell 7 MSVC developer shell for the selected target, with
Git, Rust/Cargo 1.98.1, the target Rust standard library, Windows SDK, and
build-time libclang installed:

```powershell
$env:LIBCLANG_PATH = 'C:\tools\llvm\bin'
./eng/build-wgpu-native-windows.ps1 -Rid win-arm64
# Or -Rid win-x64 in the matching x64 developer shell.
```

`-CargoExecutable`, `-RustcExecutable`, `-BuildDirectory`, and `-Jobs` permit
isolated toolchains and bounded build parallelism without persistent PATH or
system changes. A retry reuses the target cache only after checking the pinned
checkout, headers and expected metadata. Unexpected source/metadata/untracked
changes are rejected, not reset. Child failures propagate. The caller's RUSTC
and CARGO_TARGET_DIR are restored even after a failed build.

Before publication the script verifies that the lock is unchanged and the DLL
PE machine matches the requested RID. It publishes to a fresh directory with
the dependency's top-level licenses and `wgpu-native-build.json`, recording
ABI, revisions, feature, tool versions and DLL hash. This is reproducible
dependency selection, not a claim of byte-identical builds across SDK/linker
installations. The output is explicitly unqualified and never overwrites a
NuGet cache, system library or existing application payload. DXC/DXIL and WARP
DLLs are not bundled by this script.

## Applicability and remaining integration

The same native instance/device compiles shaders for the managed renderer and
C++ MIL renderer; a qualified feature build must be shared, not installed for
one renderer behind the other's resource handles. Dawn and browser devices
remain separately owned and must never receive the pinned native descriptors.
No compute workload, shader semantics, geometry, SIMD fallback, cache identity,
resource lifetime, DPI or text behavior changes in this build-only batch.

Required follow-up before product adoption:

1. Exact-ABI feature artifact packaging, complete dependency
   license review and permitted DXC/DXIL distribution; no development WARP
   redistribution. Do not accept arbitrary newer native WebGPU binaries.
2. Current-package x64/ARM64 rendering and all query families on actual loaded
   system/hardware runtimes, followed by complete source application gates.
3. Keep the independent system-WARP crash and hosted x64 wait investigated;
   do not change timeouts, use CPU geometry or advance WPF pins from this build.

The accelerated delivery sequence and ordered merge gates remain unchanged.

## Validation of this batch

The PowerShell script parses successfully. All six input checks pass on macOS
and Windows: CRLF/Unicode normalization, exact retry, preserving rejected caller
changes, UTF-8 native output under an OEM console, child exit/error restoration,
and the checked-in lock digest. The normal Windows native PR lanes now run these
checks. The first VM attempts rejected missing Git and an OEM-code-page mismatch
before building; neither modified dependency implementation. Explicit tool discovery
and UTF-8 capture resolve those setup issues. The isolated ARM64 build completes
successfully in 3m35s, including source/header/feature, lock and PE checks. Its DLL
SHA256 is `a0cdbedccc490377b94c4e7cf8506d65c85bbc0be1c6cf4d70d2a04c791806e3`.
The pinned upstream source emits eight preexisting Rust warnings; no implementation
or warning policy was patched to hide them.

The new DLL passes the full independent native owner-query fixture with the same
explicit-WARP/DXC diagnostic managed assembly and current native query DLL used
by the preceding investigation. The child exits 0 after point/repeated waits,
list/participation modes, both region families, region-first contexts and owner/
generation isolation. Actual loaded native dependency, DXC/DXIL 1.8.2502.11 and
development WARP 1.0.20 paths were observed in the child. First point, bounds and
ellipse submissions took 1522.061, 1599.800 and 2030.964 ms respectively; this is
functional reproduction, not a controlled benchmark. Local evidence is
`pinned-wgpu-native-build.json` and `pinned-compiler-query.*.log` under the task's
native-core validation artifacts.

This verifies the committed dependency build tooling, not current managed package,
system WARP, x64, hardware rendering or Showcase qualification. The system-WARP
DLL remains exactly the previously failing version/hash. At this build checkpoint,
typed compiler admission, packaging and the remaining runtime gates were still
required; the next section records the subsequently implemented selection path.

## Explicit product configuration — 2026-09-14

The next implementation batch connects the existing pinned native instance
extension through `WgpuContext.Dx12CompilerOptions`. Configuration is immutable
before instance creation. For example:

```csharp
using var context = new WgpuContext
{
    Dx12CompilerOptions = new(WgpuDx12ShaderCompiler.Dxc, @"C:\app\compiler")
};
context.Initialize(window: null);
```

Equivalent startup settings are `PROGPU_DX12_SHADER_COMPILER=auto|fxc|dxc` and,
only for explicit DXC, `PROGPU_DX12_COMPILER_DIRECTORY` with an absolute directory.
Without that directory, DXC/DXIL must be beside the actual loaded `wgpu_native.dll`.
Default/automatic retains FXC for this pinned Windows backend. No automatic DXC
or software-adapter promotion is introduced.

Forced DXC identifies the actual process-retained DLL with the Windows module API,
then checks its adjacent build manifest: schema, native/header/wgpu revisions,
ABI, feature, lock, RID and library SHA256. Both compiler libraries must exist
and match the process PE architecture. Missing/incorrect artifacts fail before
native instance creation; the stock binary cannot silently satisfy forced DXC.
The manifest is build provenance, not a signature or an untrusted-code sandbox.
Release provenance and complete dependency licensing still belong to packaging.

Explicit UTF-8 library paths are pinned only for the synchronous instance call.
The pinned [public native descriptor](https://github.com/gfx-rs/wgpu-native/blob/33133da4ec5a0174cb21539ef2d3346f75200411/ffi/wgpu.h)
selects the compiler. In the feature-enabled pinned HAL, compiler initialization
failure rejects the D3D12 backend rather than returning the feature-disabled FXC
path. The existing D3D12-only backend mask and selected-adapter check therefore
keep failure closed. No global logging callback, COM compiler mirror, binary
patch, alternate renderer, or CPU geometry path is added.

`SelectedDx12ShaderCompiler` is published after device creation and the existing
queue probe; `Dx12CompilerLibraryPath` reports the explicit path. These identify
selection, not successful compilation of every shader on that adapter. Shared
surfaces inherit their owner's compiler and cannot replace it or its libraries.
External Dawn/browser devices reject an explicit compiler request rather than
pretending to reconfigure a borrowed device. Disposal clears selection metadata.
The managed renderer and C++ MIL renderer consume the same owned device and
canonical shaders; the C++ engine does not create another compiler instance.

`ForceFallbackAdapter` is a separate explicit native WebGPU adapter requirement,
false by default. It preserves surface compatibility and is reported through
`WgpuAdapterSelectionDiagnostics`; external/shared devices cannot silently change
adapters. The existing complete native consumer accepts `--software-adapter`,
eliminating the previous patched managed assembly used to force WARP. This is
adapter selection for validation, not a new compute fallback or automatic CPU
execution policy.

Research was rechecked against WebRender, Skia/SkParagraph, Direct2D/DirectWrite,
Win2D, Vello/Parley and HarfBuzz through the
[shared retained-input research record](native-mil-hit-test-ownership.md#design-references-and-decisions).
Adopt only initialization-bound compiler ownership. Preserve lazy workload
pipelines, retained layout/scenes, culling, font/path/texture keys and eviction,
demand uploads, worker preparation, GPU batching, DPI/subpixel/hinting, fallback
and variable-font identity, and device-generation invalidation. No third-party
implementation is copied. Metadata work is startup-only O(B) file hashing for
B native-library bytes plus bounded descriptor/path work; .NET's SHA256 uses
its runtime implementation. No per-frame IO, parsing, hashing, new GPU passes
or per-primitive crossings are introduced.

### Product-selection validation

The updated backend and consumer build without warnings/errors. All 79 focused
compiler/context tests pass, including build-pin synchronization, loader metadata,
both PE architectures, wrong/missing
features and hashes, explicit paths, and external-provider rejection. The complete
rebuilt Metal consumer still passes with automatic selection.

The full Windows ARM64 consumer passes with current unpatched product assemblies,
explicit DXC and development WARP: native ABI/document/inline contracts, cubic
control hull, retained MIL rendering (38 resources, 11 draws, 174080 coverage),
and the complete owner/participation/region-first fixture. Product backend hash:
`1beb68b66c7078f0b9aebaf8277694147f9712152f6e2457735d12ac4bb8b5cf`;
native DLL remains `e696e6a9...`; dependency DLL remains `a0cdbedc...` above.
First point/bounds/ellipse submissions take 1318.047/2085.784/1292.745 ms;
these are functional observations, not controlled benchmark claims.

Real missing-manifest and missing-compiler controls fail before adapter creation.
The matched current-product system-WARP control passes rendering and still
terminates after first query submission (1588.796 ms), before readback. The
Parallels hardware control selects DXC but still fails shader pipeline creation
with `0x80004005`, followed by the native invalid-pipeline error path. Neither
failure is hidden by default compiler changes, software selection or relaxed
assertions. This strengthens compiler integration evidence but does not qualify
system/hardware execution, x64, final packages or the source Showcase application.
