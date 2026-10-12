# Dawn surface presentation policy

The desktop sample's `VSync=false` request previously changed the managed context
while its Dawn surface continued to configure FIFO. This capped the original
LOL/s workload at display refresh (ProGPU issue #366).

`IWebGpuExternalSurfacePresentationApi` is an optional extension to the existing
external surface contract. The exact surface owner receives the request and
returns its configured mode. Providers implementing only the original interface
keep their existing behavior; browser presentation does not invent a native mode.

Dawn snapshots the original surface's advertised presentation modes while copying
its format and alpha capabilities, before freeing their native storage. It uses
the existing `WgpuContext` policy: Immediate for an uncapped request when supported,
otherwise FIFO, otherwise the first advertised mode. Empty and unknown inventories
are rejected before acquiring the surface's device lifetime. A standalone Dawn
surface retains its synchronized default.

The selected values are cached per surface. A mode change reconfigures even when
dimensions are unchanged; resize, lost/suboptimal acquisition, unconfiguration
and disposal retain the existing exact surface/device ownership and render lock.
Capability selection is O(N) once at creation; configuration is O(1), with no new
frame allocations, copies, submissions, readbacks or queue waits.

`WgpuContext.ConfiguredPresentMode` and the sample's `presentMode` result field
report the owner's successful configuration. Unconfigured, lost and disposed
contexts report no mode. A FIFO fallback remains visible beside `vsync=False`;
the request alone is not evidence of uncapped presentation or throughput.

The public [native WebGPU surface contract](https://github.com/webgpu-native/webgpu-headers/blob/main/webgpu.h)
defines the advertised inventory and FIFO/Immediate presentation behavior. The
implementation reuses ProGPU's original selection policy and explicit ABI mapping;
no external implementation source was copied. This fixes policy transport through
the existing presentation owner, without changing renderer, scene, text or shader
algorithms. Managed rendering and the native renderer's Dawn presentation host
share the same `WgpuContext`; the stock provider retains its existing policy and
reports its selected mode through the same diagnostic.

Validation includes paired policy inventories, actual context dispatch through
legacy and extended providers, unchanged-size mode switches, resize, fallback,
configuration failure, device loss and exact surface retirement. Original
240-warmup/480-measure LOL/s runs (both VSync settings), uncapped Markdown Playground
and DXF CAD Viewer, matched macOS Instruments traces and normal producer CI remain
required before treating #366 as complete. Source tests alone do not establish a
performance result.
