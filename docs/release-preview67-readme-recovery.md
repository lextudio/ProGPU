# Preview.67 publication recovery

Preview.67 product source remains `915c0bded45b283163bc85489111b0d74a4d9d44`.
Its immutable tag and published package versions are preserved. The complete
[source Build](https://github.com/wieslawsoltes/ProGPU/actions/runs/38185119471)
passed all 59 jobs, and all 19 validation jobs in the original
[Release](https://github.com/wieslawsoltes/ProGPU/actions/runs/38185156962) passed.
The publisher then encountered a duplicate `README.md` in `ProGPU.Hmi` after
21 packages and 19 symbol packages had been accepted by NuGet.

Six HMI projects included both the repository README and their specialized
documentation at the same archive path. Their permanent source fix uses
`hmi-package-readme.md` for the specialized documentation. Full inventory
preflight now rejects ambiguous and duplicate archive paths before any upload.

The bounded recovery workflow admits only original artifact `11720419571`,
SHA-256 `509753d4d6a5b2aafa4fe7555bcd5b126db04c160371dd4962bd18604b8daa5b`.
It requires the original complete qualification and a separate whole successful
59-job Build of the publication recipe. The six unpublished HMI packages rename
their specialized README and update only the nuspec `readme` element. Both
original documents are checked against the exact product source before repair.
Every original runtime, symbol, notice, asset and dependency entry retains its
bytes; repository metadata still identifies the actual product build. The other
116 archives, including every symbol archive, are byte-identical to the qualified
artifact. The workflow uploads per-entry comparison and qualification receipts
alongside the packages. No product binary is rebuilt or relabeled by this recipe.

Publication completion and fresh public consumers remain tracked in
[#369](https://github.com/wieslawsoltes/ProGPU/issues/369) and the remaining-work
epic [#361](https://github.com/wieslawsoltes/ProGPU/issues/361). LibreWPF and
LibreWinForms releases follow complete producer publication and their own gates.
