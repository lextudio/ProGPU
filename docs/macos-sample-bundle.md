# macOS desktop sample bundle

Build each Apple runtime explicitly with the installed macOS workload:

```sh
dotnet build src/ProGPU.Samples.Desktop/ProGPU.Samples.Desktop.csproj \
  -c Release -f net10.0-macos -r osx-x64 -p:ProGpuAppleMediaMacOnly=true
dotnet build src/ProGPU.Samples.Desktop/ProGPU.Samples.Desktop.csproj \
  -c Release -f net10.0-macos -r osx-arm64 -p:ProGpuAppleMediaMacOnly=true
```

The desktop sample uses the Apple SDK's normal native-library relocation and code
signing. Its retired code-editor dependency is absent from the application graph,
so no special regular-expression-library install-name workaround remains.

Generated mesh samples use per-user application data on macOS. Application
startup must not add files to a signed bundle's Resources directory.

## Qualification

Clean Release builds for both Apple runtimes and launches after copying the
bundles away from build outputs are required. Use a remaining desktop workload
with its original 240 warmup and 480 measured frames. Preserve executable load
commands, bundle manifests, process module maps and strict deep code-signature
verification before and after execution; bundle files must remain unchanged.

An execution beside build outputs can accidentally resolve an unbundled native
library and is insufficient for this check. An x64 run through Rosetta does not
establish independent Intel GPU pixel parity or qualify every sample feature.
See [the original report](https://github.com/wieslawsoltes/ProGPU/issues/365).
