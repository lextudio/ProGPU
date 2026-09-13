# Windows shader compiler dependency

## Acceptance path and scope

`ProGPU.Wpf.ShowcaseApp` startup and first source input are blocked by slow
FXC compilation and a separate installed-WARP execution failure. The matched
[query compiler/runtime investigation](native-hit-query-segment-lanes.md)
shows that the exact-ABI DXC-capable dependency executes the complete owner
fixture with development WARP, with family submissions around one second.
System WARP still crashes with either compiler. Neither a compiler switch nor
the testing-only WARP package qualifies the application or permits a merge.

`eng/build-wgpu-native-windows.ps1` now builds that optional dependency from
pinned inputs. This is build tooling, not automatic backend selection, a new
renderer, or a replacement runtime in the product package. The stock Silk
native dependency and all existing application/CI gates remain unchanged.

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

1. Typed, immutable-before-device compiler configuration and diagnostics that
   distinguish requested compiler from actual admission. Forced DXC must fail
   rather than silently falling back when its feature or runtime is missing.
2. Exact-ABI feature artifact packaging/loader identity, complete dependency
   license review and permitted DXC/DXIL distribution; no development WARP
   redistribution. Do not accept arbitrary newer native WebGPU binaries.
3. Current-package x64/ARM64 rendering and all query families on actual loaded
   system/hardware runtimes, followed by complete source application gates.
4. Keep the independent system-WARP crash and hosted x64 wait investigated;
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
DLL remains exactly the previously failing version/hash. Typed compiler admission,
normal product packaging and the remaining runtime gates above are still required.
