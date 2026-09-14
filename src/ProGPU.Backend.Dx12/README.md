# ProGPU Windows shader compiler runtime

`ProGPU.Backend.Dx12` supplies the pinned DXC-capable WebGPU dependency, DXC/DXIL,
provenance and licenses. It is an optional companion to `ProGPU.Backend`, shared
by managed and C++ renderers. It contains no WARP implementation.

Executable consumers must select `win-x64` or `win-arm64` explicitly. The build
selects this package's dependency instead of Silk's stock Windows DLL; it never
edits the NuGet cache or system files. Build, publish and NativeAOT use the same
selection. Non-Windows RIDs remain unaffected. A missing RID is rejected for
executables; library projects may carry the package transitively.

Select DXC before device creation through `WgpuDx12CompilerOptions` or
`PROGPU_DX12_SHADER_COMPILER=dxc`. Compiler DLLs and their manifest are beside
the selected WebGPU library, so no machine-specific compiler directory is needed.
The package does not change automatic compiler/query defaults. Explicit ordered
queries use `PROGPU_HIT_TEST_EXECUTION=ordered-stages`.

Package presence is not runtime qualification. Consult the release's Windows
adapter/package evidence before deployment. Keep all bundled notices with the
published application.
