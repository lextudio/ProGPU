# macOS desktop sample bundle

Build each Apple runtime explicitly with the installed macOS workload:

```sh
dotnet build src/ProGPU.Samples.Desktop/ProGPU.Samples.Desktop.csproj \
  -c Release -f net10.0-macos -r osx-x64 -p:ProGpuAppleMediaMacOnly=true
dotnet build src/ProGPU.Samples.Desktop/ProGPU.Samples.Desktop.csproj \
  -c Release -f net10.0-macos -r osx-arm64 -p:ProGpuAppleMediaMacOnly=true
```

Onigwrap 1.0.10's Intel library has insufficient Mach-O header space for the
Apple SDK's default executable-relative install name. The desktop project gives
only this dependency the shorter `@rpath/libonigwrap.dylib` identity. The SDK's
existing MonoBundle search path resolves it. Normal private-copy relocation,
native executable linking and code signing still run; the NuGet asset, package
version and original notices are unchanged.

The SDK's relocation timestamp check does not include identity metadata. A
project change therefore invalidates only the SDK-owned Onigwrap copy. A stamp
is written after successful relocation. The copy stays in the SDK's common
native directory so ordered `-l` searches cannot select an older same-name
library. Existing explicit identity/path metadata is preserved.

Generated mesh samples use per-user application data on macOS. Application
startup must not add files to a signed bundle's Resources directory.

## Qualification

The original Intel failure and successful ARM64 control were reproduced with
.NET SDK 10.0.201, Microsoft.macOS.Sdk 26.4.10259 and Xcode 26.4.1 (17E202).
Both repaired Release bundles were copied away from build outputs and launched
on macOS 26.6: ARM64 natively and x64 through Rosetta. Each completed the Visual
Designer workload with 240 warmup and 480 measured frames. Process module maps
confirmed the relocated bundle's own Onigwrap library was loaded. Every bundle
file remained byte-identical after execution, and strict deep code-signature
verification passed both before and after.

An execution beside build outputs can accidentally resolve an unbundled native
library; it is insufficient for this check. Preserve build logs, original
dependency hashes, executable load commands, relocated bundle manifests and
module maps. This qualifies bundle construction and loading, not independent
Intel GPU pixel parity or every sample feature. See
[the original report](https://github.com/wieslawsoltes/ProGPU/issues/365).
