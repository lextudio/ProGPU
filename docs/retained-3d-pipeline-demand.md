# Retained 3D pipeline demand and line bindings

The native engine previously created all nine 3D pipelines when compiling its
first retained 3D page. The existing compiler converts source triangle strips
to triangle lists, so even the three strip variants were created without draws
that could consume them. The managed mesh extension similarly created front,
back and wireframe pipelines together, and both unfilterable variants whenever
either material orientation required one.

Both implementations now collect requirements from the same drawable records,
material selection and canonical topology used by their encoders. Each existing
pipeline is created on its first actual demand and retained by its original
device/engine owner. Shader bytes, pipeline raster/depth/blend descriptors,
primitive conversion, geometry, source clipping, sample counts and rendering
order are unchanged. The native compiler finishes inventory validation and
pipeline acquisition before replacing its previous retained page buffers.

Managed bindings follow that demand too. Visible edge passes still require the
solid resource group in wireframe mode. Buffer or sample-count changes retire
both old groups, including the temporarily unused group, before a later mode
can consume it. A failure leaves the generation invalid for retry. Warm frames
classify the retained records even when they upload no new record bytes.

## Line-only native correction

A new line-only test exposed a separate native validation error: the line
pipeline used the two-group mesh layout although the line shader consumes only
group zero. A preceding mesh could supply the missing group and hide the error.
The line pipeline now owns a one-group layout. Its creating engine releases that
layout after its pipelines and before the shared bind-group layouts.

Both native providers compile the same implementation. The managed
`Line3DExtensionPipeline` already has its own line shader and layout and does
not depend on a preceding mesh binding; the layout correction is native-only.
The mesh demand change applies to both `Mesh3DExtensionPipeline.cs` and
`progpu_native_3d_execution.cpp` and is implemented in both.

## Validation and limits

The additive WebScene 3D test forwards the real provider pipeline-creation
procedure without changing descriptors and observes successful creation labels.
Two independent engine owners each exercise seven scenes and cold/warm replay:
line-only, ordinary mesh, front/back material, canonicalized strips, visible and
occluded edges, and return to line-only. All 28 complete RGBA frames are retained;
warm uploads are zero and no variant is created twice within an owner.

The original library rejects line-only rendering with a missing group-one
validation error. A separately selected baseline diagnostic compares its five
previously supported mixed scenes: all 20 complete frames match the candidate
byte for byte in baseline/candidate/candidate/baseline order. The ordinary new
test always runs all seven scenes. Neither diagnostic replaces the original
complete WebScene corpus, its 60-second limit or any original assertion.

Managed GPU regressions exercise front-first and wireframe-first ownership,
late back-material demand, exact oriented front/back and cold/warm pixels,
buffer growth while wireframe edges are active, return to the original frame,
and exact successful pipeline counts. Existing planar/P010/RGB materials,
retained uploads and native interoperability checks are also retained.
Local Release validation passed 152 focused managed/interop tests, the complete
WebScene corpus, the additive 3D test, and eight timeout-diagnostic controls.

Matched Time Profiler attachments to the original complete native workload
completed successfully and exported 4,885 baseline and 3,406 candidate samples.
Their attachment intervals do not capture complete process startup. Earlier
launch-mode traces produced no usable samples and are not performance evidence.
A subsequent 12-process uninstrumented comparison completed ten successful
original corpora before an external-volume file-open stall interrupted the
remaining work. That incomplete comparison is not a passing performance gate.
There is no claimed application speedup, memory improvement, tail-latency result
or hosted timeout resolution from these local observations. Complete profiling,
the whole producer Build and downstream package/application checks remain
required before release qualification.

## Research and provenance

Implementation provenance is ProGPU's original 3D page compiler, shader layouts,
retained WebScene fixture, managed mesh extension and pipeline cache at
`9873da2149939b1de80c003b41c06f6c53e477b4`; no external implementation was copied.
The primary references and architectural boundaries in
[pipeline creation diagnostics](render-pipeline-startup-diagnostics.md#research-boundaries-retained)
were revisited: Skia/SkParagraph, Direct2D/DirectWrite, Win2D, WebRender,
Vello/Parley and HarfBuzz. Their applicable resource reuse, deferred preparation
and separation of shaping/layout from raster work support retaining exact owned
resources and compiling only actual work. This change does not alter text layout,
glyph or path rasterization, tiling, batching, atlas keys, eviction, invalidation,
parallel preparation, fallback fonts, DPI or subpixel policy.
