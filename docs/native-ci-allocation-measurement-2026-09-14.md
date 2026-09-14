# Native image-effect allocation measurement

Build run `34786219474`, Linux job `103802076430`, at `45147156` reports
4,576 passing tests, seven skips and one failure:
`SemanticImageEffectBuildsWithoutAllocation` measured 720 bytes against its
zero-byte requirement. This is separate from the Windows native query failure.

The fixture took its second allocation-counter reading after `Assert.True`.
It now captures the delta immediately after the builder loop and only then
asserts success and zero bytes. The same 10,000 calls, source payload, warm-up
call, validation and exact allocation threshold are preserved. No product code,
test exclusion, retry-to-pass behavior or tolerance is changed. This removes
test-framework execution from the measured region; it does not prove where the
reported 720 bytes originated. A current-head Linux CI result remains required.

The original focused test passes in a fresh local Metal-hosted test process.
With the corrected measurement boundary, all 122 native interop tests pass on
the same host. Neither result is presented as Linux qualification.
