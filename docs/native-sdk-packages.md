# Native runtime and static SDK packages

Starting with preview.67, `ProGPU.Backend.Native` depends on the matching
`ProGPU.Native.Sdk` package. The backend retains the managed host, all six desktop
runtime closures, headers, C++ module interfaces, CMake configuration and runtime
notices. The companion contains the original static SDK archives, complete
private FreeType/ICU receipts and original notices. Neither package redistributes
a placeholder native runtime or substitutes an ambient dependency.

The split keeps the ordinary NuGet ZIP format and original payload bytes. The
preview.66 package was 298,768,221 bytes; lossless standard-DEFLATE recompression
still exceeded NuGet.org's request limit. The new layout leaves room below that
limit without changing rendering, font data, dependency receipts or source input.
Preview.66's immutable tag and partial public package history remain preserved in
[the publication recovery issue](https://github.com/wieslawsoltes/ProGPU/issues/369).

Ordinary NuGet restore downloads the SDK dependency automatically. CMake reads
the runtime package's generated version record and finds that exact SDK version
in the standard neighboring NuGet cache directory. It verifies the SDK's version
record before importing static targets. Existing dependency checks then verify
the target architecture, archive hashes, pinned private linkage and original
notices. No newer cached version or system ICU/FreeType is used as a fallback.

For manual package extraction, extract the two matching packages into separate
directories and select both explicitly:

```sh
cmake -S consumer -B build \
  -DProGPUNative_DIR=/absolute/native-package/build/native/cmake \
  -DPROGPU_NATIVE_SDK_ROOT=/absolute/sdk-package
```

A regular CMake install retains its existing `include`/`lib` layout. The original
C++ package consumer still links the exported MIL and Dawn targets, with their
original transitive dependencies. The drawing package group includes the SDK
before the native backend so its local feed has the complete versioned closure.

Both NuGet projects use the same original staging validation. Build and release
consumers still require real artifacts from the exact successful whole producer
Build. Layout controls verify actual Pack items, original file paths/bytes,
extensionless notices, version resolution and rejection. These checks do not
replace the full native, managed, package or application gates.

Package verification rejects empty, duplicate, linked or oversized upload files.
The release preflight checks runtime, Avalonia and symbol packages together before
the first push, reserving 64 KiB beneath the 250 MiB request limit for overhead.
