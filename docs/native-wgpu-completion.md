# Native queue completion and raster retirement

## Blocking application path

Acceptance application: `ProGPU.Wpf.ShowcaseApp`. The first native frame must
finish before its temporary raster resources are retired; native owner queries
must consume completed map callbacks, not partially executed shader output.

Run `34780425843`, x64 job `103786318970`, isolates the failure on package
`0.1.0-preview.3013.ci`: the retained raw reference passes, but releasing raster
resources after a blocking poll returning 1 causes DeviceLost during readback.
The direct native frame is black; the identical native draw passes when its
resources remain alive through the existing synchronized target readback. ARM64
job `103786318840` completes successfully with all probes passing, demonstrating
adapter/timing sensitivity rather than reliable x64 qualification.

## Runtime contract finding

ProGPU pins wgpu-native `33133da4ec5a0174cb21539ef2d3346f75200411`, whose Cargo.lock
selects wgpu-core `87576b72b37c6b78b41104eb25fc31893af94092` (0.19.4). Inspection of
that dependency confirms that its blocking maintenance path advances retirement
to the requested index after the internal timed wait, without checking whether
the fence reached it. Its nonblocking path reads the actual fence value.
The observed five-second poll duration is consistent with the upstream
[timeout defect](https://github.com/gfx-rs/wgpu/issues/4589). This explains why a
reported queue-empty result is insufficient on that blocking path.

This investigation uses upstream behavior/API documentation and read-only source
inspection; no dependency implementation is copied, patched or vendored. The
repair is original ProGPU scheduling code around the existing C API.

## Paired repair

- C++ submission waiting repeatedly requests nonblocking fence progress, sleeping
  one millisecond between pending results. Nonwaiting callers still poll once.
  Queue-empty conservatively proves the validated token and preceding work done.
- Managed `WgpuContext.WaitIdle` follows the same rule before publishing its
  drained count or releasing external resource owners.
- Native hit-test waits use nonblocking progress and the existing atomic map
  callback state. Cancellation cleanup drains the actual queue before releasing
  its pending resources. Dawn future waits and browser asynchronous admission
  remain unchanged.

No new submissions, CPU geometry, shader forks, warm-up frames, scalar fallback
or managed/native crossings are introduced. The existing synchronous wait
contract stays synchronous; no elapsed-time value is promoted to success.
Existing host/readback qualification deadlines and source identity remain intact.
Polling uses O(1) state and O(P) API calls for P pending checks, with a sleeping
thread rather than a busy spin. Immediate completion adds no sleep. This is
synchronization, not a data-parallel CPU kernel requiring SIMD.

## Validation

Both C++ providers compile. All 20 native CTest entries and 49 focused managed
completion/retirement tests pass. The rebuilt Metal package consumer passes its
original frames and all owner/generation/participation/region-first assertions.
The new `--path-native-fence-retire-raster-probe` also passes exact target and
atlas checks after real completion followed by raster release. Its bounded
diagnostic loop fails at 30 seconds; it never turns a timeout into completion.
The manual workflow's `fence` group can apply that comparison to the original
failing package without qualifying it as a new release.

Windows runtime, final-head package CI and full application qualification remain
required. Earlier query pipeline crashes and latency are not declared resolved
solely from this synchronization repair. Broader Direct2D/Win2D remains deferred.
