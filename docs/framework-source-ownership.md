# Framework source ownership and reproducible builds

Framework-specific source is maintained by its framework repository:

| Source owner | Projects | Public identity |
| --- | --- | --- |
| LibreWPF | `src/ProGPU.Wpf.Interop` | `LibreWPF.Interop` package; `ProGPU.Wpf.Interop` assembly |
| LibreWPF | `src/ProGPU.Compatibility/WindowsBase`, `PresentationCore` | Existing private compile-time assembly identities |
| LibreWinForms | `src/ProGPU.Drawing/System.Drawing.Common`, `.Tests`, `.Benchmarks` | `ProGPU.System.Drawing.Common` package; `System.Drawing.Common` version `10.0.0.0` |

The move preserves original implementation/test bytes, public types, signing key
and package IDs. The original files and history remain available in ProGPU commit
`3f1454a01dba3d6338c56fabfde7bf6a160c5c62`. Independent Microsoft reference inputs
are unchanged. Native/managed renderer algorithms and platform admission are
unchanged; this work changes source ownership and the build/package graph.

Before a source build, run:

```sh
git submodule update --init --recursive
python3 eng/progpu-prepare-framework-sources.py
dotnet build src/ProGPU.Samples.Desktop/ProGPU.Samples.Desktop.csproj -c Release
```

`eng/framework-sources.json` records full LibreWPF and LibreWinForms commit SHAs.
The preparer creates sparse, detached checkouts under `external/LibreWPF` and
`external/LibreWinForms`, without initializing their nested ProGPU submodules.
It rejects dirty source checkouts, unexpected remotes and symbolic-link targets.
These source dependencies are ordinary disposable build inputs; they are not
additional development worktrees. Keep local changes before refreshing them.

Local coordinated development can supply `LibreWpfSourceRoot` and
`LibreWinFormsSourceRoot` as MSBuild properties, or environment variables for
the packaging scripts. Moved projects receive the selected `ProGpuSourceRoot`;
all core dependencies then refer back to that one producer. Direct project builds
also recognize the documented producer sparse-checkout layout. A standalone
framework checkout uses its own pinned ProGPU submodule.

ProGPU continues to assemble the existing package closure from these recorded
source owners. No package is renamed and no dependency is dropped: Drawing still
depends on the real Native and Dawn backends and their exact static SDK/runtime
payloads. Package metadata and README identify the source owner. Consumers retain
exact version, strong-name, source/package hash and native receipt checks.

The whole producer Build must succeed before consumers stage native payloads.
Both framework repositories still require their complete source/package and
application gates before release. A source relocation or isolated project build
does not qualify native rendering, Display text, input, or desktop behavior.
