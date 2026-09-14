# Windows DX12 compiler runtime package

Acceptance: **ProGPU.Wpf.ShowcaseApp**, native pointer/region queries after
startup. The blocking source path is the stock Windows WebGPU dependency's FXC
compiler. Ordered queries pass with DXC on system WARP, but a diagnostic compiler
directory is not a deployable dependency graph.

## Package and selection

`ProGPU.Backend.Dx12` is an optional native-assets-only package shared by managed
and C++ renderers. It carries both `win-x64` and `win-arm64` feature-enabled WebGPU
libraries, pinned redistributable DXC/DXIL, build/package receipts and original
notices. DXC package metadata is retained as `.package.xml`, because NuGet reserves
`.nuspec` files during packing. No WARP implementation is included.

Executables must select their RID. For Windows RIDs the transitive target removes
only Silk.NET.WebGPU.Native.WGPU's known `wgpu_native.dll` from build and publish
asset lists and supplies this payload. Other packages/caller assets and non-Windows
RIDs are unchanged. Two native assets cannot compete for the same output path.
No NuGet cache or system DLL is edited. Native runtime files and manifests remain
external even for single-file publication. Libraries may propagate the package.

Compiler/query selection remains explicit:

```text
PROGPU_DX12_SHADER_COMPILER=dxc
PROGPU_HIT_TEST_EXECUTION=ordered-stages
```

No compiler-directory environment variable is required: the compiler is beside
the actual loaded WebGPU DLL. Existing typed configuration and loaded-module
identity/hash/architecture checks remain authoritative. Package installation does
not change automatic policy or qualify every Windows adapter.

## Production and guards

`eng/build-progpu-dx12-runtime-windows.ps1 -Rid win-x64|win-arm64` uses the existing
exact-ABI builder with pinned Rust/Cargo and locked dependencies, followed by
signed/hash-pinned compiler staging. The dependency builder now accepts a fresh
explicit artifact destination so CI does not guess generated directory names.
It does not change the default toolchain or upstream renderer implementation.

`eng/stage-dx12-runtime.ps1` validates matching RID, ABI/header/source/feature/lock
receipts, compiler package/author pin, three binary hashes and PE architecture.
It copies an exact allowlist into a fresh temporary directory, rechecks copied
binary hashes, then publishes atomically. Missing/modified inputs fail before
publication. Package validation requires both RIDs, binaries/receipts/notices and
rejects WARP before NuGet generation, not after writing a package.

Build CI creates the two payloads alongside existing native builds, includes the
optional package in the native artifact, and adds full Windows x64/ARM64 system-
WARP JIT and NativeAOT consumer jobs using actual packages. Existing default gates
remain unchanged. The shipping manifest, portable pack and release workflow also
include this package; NuGet publication requires both added Windows JIT/NativeAOT
release consumers. Runtime automatic selection remains unchanged.

## Evidence — 2026-09-14

- x64 dependency cross-build in the Windows ARM64 VM: 2m14s, pinned ABI and PE
  verified. DLL SHA-256:
  `5777347a165be3d640424b48f2eab8e11523d678c381e9ca1373fdd7ff9101c3`.
  This is compilation, not x64 runtime qualification.
- Both compiler payloads pass signature, pinned hash and PE validation. The
  combined NuGet packs without warnings, retaining both RIDs and notices.
- Nine runtime-file checks pass (hash, architecture, malformed/truncated PE);
  three MSBuild selection cases pass for Windows x64/ARM64 and Linux. Caller-
  owned same-name assets are retained. Missing executable RID rejects; missing
  package RID rejects before a NuGet file is produced.
- Actual NuGet restore/publish selects the expected ARM64 WebGPU/compiler hashes.
  The full native consumer passes on system WARP: retained MIL pixels, original
  owner/generation checks and region-first queries. No external compiler directory
  is set; all four actual loaded module paths are checked. Stdout SHA-256:
  `094e4fc40c85637cfe761a6f321574152c20ab42a93ce04035d2589e71f28f75`.
- This local run restores the DX12 package but still uses project-reference
  managed/C++ renderer builds. Complete CI package graph, x64 runtime, NativeAOT,
  hardware adapters and Showcase source/application gates remain required. This
  does not resolve stock-FXC X3511 or authorize a default switch/merge.

Artifacts: LibreWPF `artifacts/dx12-package.toCGR2`. No VM configuration/system
files were changed. Broader Direct2D/COM/Win2D expansion remains deferred.

## Release inventory and hosted compiler repair

At `dec74b5b`, documentation CI correctly rejected the unclassified new project.
The shipping manifest now includes it, with the audit count increased by exactly
one to 81. Portable and release packing receive both compiler-runtime artifacts;
asset-only packing produces no empty symbols. The package verifier checks both
RID file inventories, original notices and absence of WARP/placeholder assemblies.
Release publication additionally requires full Windows DX12 JIT/NativeAOT jobs.

The x64 compiler-runtime job at that head passed. ARM64 failed with 247 missing-
field errors in generated bindings, before any runtime test. Its hosted image
ships [LLVM 22.1.8](https://github.com/actions/runner-images/blob/win11-arm64/20260906.161/images/windows/Windows11-Arm64-Readme.md).
That symptom matches the documented [bindgen Clang-22 typedef regression](https://github.com/rust-lang/rust-bindgen/issues/3275),
not a change to ProGPU's C ABI. The build now selects ClangSharp's signed
`libclang.runtime.win-x64` / `libclang.runtime.win-arm64` 18.1.3.1 packages,
matching the previously successful VM generator. The NuGet metadata identifies
LLVM 18.1.3 and Apache-2.0 WITH LLVM-exception; no foreign implementation is copied
into ProGPU code.

`eng/wgpu-dxc/libclang-package.json` pins package hashes, DLL hashes and the
ClangSharp author certificate. Both actual packages pass signature verification
and fresh allowlisted staging locally. The dependency builder checks the build
host's DLL hash and records its version/hash/host RID; it no longer admits ambient
libclang. This tool is build-only and never included in the DX12 runtime package.
The existing six input tests and both workflow lint checks pass. Full local docs
verification still requires the absent ACadSharp submodule; the new hosted run
must prove complete docs, package graph and ARM64 build/runtime qualification.
