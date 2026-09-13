# Native path first-frame isolation

## Blocking acceptance path

The core application is `ProGPU.Wpf.ShowcaseApp`; the user action is its first
native MIL path drawing. The source path is retained MIL geometry through the
native path coverage buffer, R8 atlas and analytic draw. At `e56d45c5`, both
Windows package jobs fail the original cubic/independent-rectangle assertion
with an entirely black target, before any hit query. Forty other PR checks pass.
The package jobs remain required and no first frame may be discarded or retried
as a successful substitute.

## Bounded diagnostic

`ProGPU.Native.PackageConsumer --path-coverage-probe` executes the packaged
`ProGPU.Backend.Shaders.PathRasterizerShader`, entry `cs_main_ordinary`, with
its original five-binding storage ABI, 16x16 workgroup and eight-by-eight sample
grid. A closed four-segment rectangle fills a 64x16 coverage region, packed at
256 bytes per row. The same command buffer copies that region to `(17,19)` in a
new 1024x1024 R8 atlas. Readback then compares known interior/exterior samples
from both the raw coverage and copied texture, plus an untouched atlas sample.
Expected values are `(255,0)`, `(255,0)` and `0` respectively.

This is an original test over existing ProGPU source, not a shader reduction,
CPU rasterizer, new runtime API or production fallback. It separates basic
canonical rasterization from partial atlas transfer. It does not exercise the
native semantic material bindings, cubic math, fragment sampling or original
failed frame, and cannot qualify those contracts.

CI invokes it in a separate process only after a Windows package job has
already failed. The original assertion, exit status, job deadline, compiler,
dependencies and later qualification gates are unchanged. A successful
diagnostic does not make the failed job successful. No production warm-up,
extra submission or readback was introduced.

## Local results, 2026-09-13

- Release build: zero warnings/errors.
- Metal, Apple M3 Pro: exact expected samples, exit 0.
- Exact package `0.1.0-preview.3000.ci`, Windows ARM64, Parallels Display Adapter:
  exact expected samples, exit 0.
- Same packaged original wgpu runtime, forced Microsoft Basic Render Driver:
  exact expected samples, exit 0. Only the isolated diagnostic Backend assembly
  selects the software adapter; it is not shipped.

Logs remain in `artifacts/path-coverage-probe-{build,metal}.log` in the ProGPU
worktree and the parent workspace's
`artifacts/native-windows-consumer.akdIwM/path-coverage-{normal,warp}.log`.
Hosted CI evidence is still required; the VM has a different Windows graphics
stack. Do not claim that the partial transfer hypothesis is resolved on CI.

## Research boundary

The upstream [wgpu driver issue inventory](https://github.com/gfx-rs/wgpu/wiki/Known-Driver-Issues)
distinguishes historical WARP descriptor-lifetime faults from AMD partial-texture
update faults. [Issue 1306](https://github.com/gfx-rs/wgpu/issues/1306) records the
latter behavior; [issue 1002](https://github.com/gfx-rs/wgpu/issues/1002) records
the former. These motivate isolating observable transfer behavior, not a claim
that an old driver defect causes this failure. No foreign implementation was
copied, and no speculative full-atlas clear, driver replacement or descriptor
workaround is introduced.
