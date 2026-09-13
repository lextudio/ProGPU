# Native owner-query WARP isolation

Acceptance application: `ProGPU.Wpf.ShowcaseApp`. User action: pointer/region
selection over a presented source visual. Blocking path: the canonical native
owner query before its first map callback. Bounded outcome: distinguish initial
shader execution from repeated query reuse without changing query semantics.

## Independent fixture

The package consumer's `--native-owner-query-probe` creates a fresh context and
native compositor, then runs the **entire existing** owner-snapshot fixture.
It preserves first-point ownership, 16 repeated waits, scene-generation isolation,
result capacity rejection, list/no-list semantics, source participation and fresh
region-first contexts. The shader, exact geometry and readback deadline are
unchanged. First-readback and repeated-wait milestones locate a crash without
claiming success from submission alone. The ordinary consumer still runs this
fixture after rendering.

Both the Release build (zero warnings/errors) and complete independent fixture
pass on Apple M3 Pro / Metal. This is not Windows or full application admission.

## System WARP first-chance evidence

The staged ARM64 consumer uses the repaired C++ DLL SHA256
`902431121dda4d2b189efbf2300950b05486f1751a8eff8bfec42a11d22c6248`.
Its managed assembly is the earlier WARP-selecting diagnostic, not the final
managed package. System WARP reports `10.0.26100.9278`.

With a fresh context, submission takes 19,879.905 ms and execution raises
`0xC0000005` before the first completed-readback milestone. ProcDump `-mm -e 1
-f C0000005 -n 1` captures the first chance; no system debugger registration,
Windows Error Reporting configuration or VM settings are changed.

In `dotnet.exe_260913_224509.dmp`, faulting thread 9 has PC
`0x7df4b37215e0`, instruction `str w11, [x28]`, and `x28=0x2144`.
The preceding instructions set that offset and add it to the zero register;
adjacent stores use SP-relative locations. LR `0x7ffa024e71e4` resolves inside
`C:\Windows\System32\d3d10warp.dll`, base `0x7ffa02450000`.
This is evidence of an invalid generated-code store, consistent with faulty
stack-spill addressing in WARP; it is not evidence of a managed map callback
invoking address zero. The earlier terminal unknown-module/zero-offset event
was insufficient to locate this first-chance failure.

## Controlled development-runtime comparison

Microsoft's [WARP package](https://www.nuget.org/packages/Microsoft.Direct3D.WARP)
1.0.20 documents code-generation fixes including ARM64. It is used only for an
isolated Windows development comparison, not added to ProGPU packages or copied
over Windows system DLLs. Microsoft describes the
[app-local testing mechanism](https://github.com/MicrosoftDocs/win32/blob/docs/desktop-src/direct3darticles/directx-warp.md).
The package's testing-only license and redistribution restriction prevent treating
this diagnostic binary as a redistributable product repair.

Package SHA256:
`e5fe5de661ce98b58ef9cfb736e73c0a7a2623d3bbf5f14839b2d55566d87e40`.
ARM64 DLL SHA256:
`361df6b6b7afda7afaab66d11344dfecbc11d89169815144a6e2a7a3ab82ae23`.
File version: `1.0.20.0.20260527.5`. Its Microsoft Authenticode signature is
validated before execution.

Copy the existing built consumer directory to a new isolated directory, place
the package's ARM64 `d3d10warp.dll` next to its apphost, and run the apphost with
`--native-owner-query-probe`. Do not launch via the shared system `dotnet.exe`
and assume that it uses the app-local DLL. Inspect the child process's loaded
module path; this comparison confirms the isolated DLL is actually loaded.
Keep the source/native assemblies and shader identical, collect stdout/stderr
and the real child exit code, and retain the original system-WARP failure.

The development-runtime run submits the first point query in 22,532.337 ms,
completes its readback in 155.797 ms, and passes all 16 repeated waits. The
first bounds-region pipeline is still running at this checkpoint, before its
submitted milestone, with CPU time increasing beyond 218 seconds. This exposes
cold region-query latency separately from the initial point execution crash;
it is not a completed region query, timeout pass or full-fixture qualification.

Do not fix this crash by replacing the native index with managed/CPU geometry,
removing shader families, widening deadlines or declaring pipeline submission a
successful query. Final system-runtime compatibility, exact current-package CI,
source-host input and full application/platform qualification remain required.
