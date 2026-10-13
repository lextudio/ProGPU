# WebScene timeout evidence

The original WebScene exact-provider test is the acceptance application. Its
unchanged CTest command retains all source fixtures, pixel/counter assertions,
the pinned provider, GPU_COMPLETE presentation and the original 60-second limit.
Issue367 requires actual hosted stack evidence before changing renderer or
presentation behavior; a faster local run does not explain hosted timing.

The verification script now preserves its original CTest output and exit status
through `eng/progpu-webscene-timeout-diagnostics.py`. Successful runs are neither
sampled nor replayed. Only an original CTest timeout starts one separate diagnostic
replay of the same executable/provider. It has its own 60-second process bound,
fresh output directory, input hashes and `qualified:false` receipt. macOS `sample`
collects up to 55 seconds at a 10-millisecond interval. Target and collector are
owned and retired independently. Collector failure is retained; it cannot repair
the original gate. There is no retry-until-pass or timeout extension.

Build and Release upload the original evidence and separate diagnostic directory
even on failure. A successful diagnostic replay still returns the original failed
CTest status, preventing package production and the managed consumer from using
an incomplete producer. Stack residency and measured phase costs remain evidence
for investigation, not proof of a particular performance cause.
