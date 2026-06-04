# PR Summary: Fix Rendering Invalidation, Memory Leaks, and Add Compatibility Shims

This Pull Request introduces significant performance optimizations, architectural stability enhancements, and advanced vector/text composting features to the ProGPU graphics engine. It also adds three new compatibility shims (SkiaSharp, GDI+, and WPF DrawingContext) to allow drop-in replacement on the GPU.

---

## Key Achievements & Implementation Details

### 1. Deferred Native Resource Disposal & Memory Safety (Backend)
- **Problem**: Direct disposal of WebGPU handles (`GpuTexture`, `GpuBuffer`) on arbitrary garbage collection finalizer threads caused deadlocks or thread-unsafety crashes because WebGPU commands must run on the render thread.
- **Solution**: Implemented a thread-safe deferred disposal queue inside `WgpuContext`. Native handles are queued during garbage collection finalization, and the render loop thread safely drains and releases them synchronously during the frame render cycle. This resolved a memory leak of up to **28.6 GB** under continuous redraw tests.
- **Cleanup**: Removed redundant/unsafe custom finalizers from managers like `ComputeAccelerator` and `GlyphAtlas`.

### 2. Precise Direction-Aware Winding Intersection Rules (Anti-Artifacting)
- **Anti-Artifacting**: Replaced standard winding interval calculations in `GlyphRasterizerShader` and `PathRasterizerShader` with **Precise Direction-Aware Half-Open Winding Intervals** to prevent dropout seams and horizontal line artifacts at curve/line boundaries:
  - **Upward Crossing (`deriv_y > 0.0`)**: Evaluated on `[0.0, 1.0)` (inclusive of start, exclusive of end).
  - **Downward Crossing (`deriv_y < 0.0`)**: Evaluated on `(0.0, 1.0]` (exclusive of start, inclusive of end).
- **Optimization**: Integrated a pipeline cache in `RenderPipelineCache.cs` to reuse compiled WebGPU shader pipelines and avoid compilation overhead.

### 3. Advanced Compositor & Scene Features
- **Blending & Clipping**: Implemented a blend mode stack, hierarchical geometry clipping masks, and opacity masks in `Compositor.cs`.
- **Offscreen Texture Caching**: Integrated offscreen texture caching to optimize rendering performance for nested visual layers and prevent redundant drawing of unchanged sub-trees.
- **Winding Correction**: Fixed boundary crossing mathematical logic within the `Hatch` shader to align with the precise winding rules.

### 4. Text & Vector Optimization
- **Outline Caching**: Added outline and Y-axis flipped outline caches (`_glyphOutlineCache` and `_flippedOutlineCache`) in `TtfFont.cs` to avoid parsing glyph geometries from TTF byte arrays multiple times.
- **Glyph Flipping**: Implemented `GetFlippedGlyphOutline` to flip coordinates along the Y-axis, conforming with WebGPU screen space coordinate projection.
- **SVG Arc Support**: Implemented SVG elliptical arc (`'A'`/`'a'`) path segment parsing in `PathGeometry.cs` and compilation in `PathAtlas.cs`.

### 5. UI Controls & Hosting
- **Corner Radius**: Added `CornerRadius` support to `ProGpuHostControl` with rounded-rectangle clip path pushes in the Avalonia `Render` override.
- **Context Reuse**: Updated `ProGpuHostControl` to share the active `WgpuContext` instance.
- **Type Safety**: Refactored the UI composition communication to use a strongly-typed `RenderState` struct instead of a generic `Tuple`.

### 6. SkiaSharp Compat Shim API
- **Drop-in Parity**: Created a swappable baseline SkiaSharp API dependency mapping matching Avalonia's minimal rendering requirements (canvases, surfaces, typefaces, paints, paths, and text blobs).
- **ReadPixels Support**: Implemented native texture pixel readback (`ReadPixels` in `GpuTexture.cs`) utilizing asynchronous mapping to pull unpadded frame pixels back to the CPU cleanly.

### 7. GDI+ Compat Shim API (`System.Drawing.Common`)
- **Graphics & Vectors**: Implemented a swappable class library for GDI+ (`System.Drawing.Common`) mapping `DrawLine`, `DrawRectangle`, `FillRectangle`, `DrawEllipse`, `FillEllipse`, `DrawPolygon`, `DrawPath`, `DrawString`, `MeasureString`, and `DrawImage` directly onto the GPU command queue.
- **Offscreen Bitmaps**: Backed `Bitmap` and `Image` entirely by WebGPU texture targets. Drawing is accumulated on the GPU and read back only when requested.
- **Diagnostics Preview**: Created a visual page (`GdiShowcasePage.cs`) to showcase GDI drawings and operations in the gallery app.

### 8. WPF DrawingContext Compat Shim API (`PresentationCore`)
- **WPF Parity**: Added a swappable class library (`PresentationCore`) under the standard WPF namespaces (`System.Windows`, `System.Windows.Media`, `System.Windows.Media.Imaging`).
- **Media Primitives**: Maps standard primitives (`DrawLine`, `DrawRectangle`, `DrawRoundedRectangle`, `DrawEllipse`, `DrawGeometry`, `DrawImage`, `DrawText`, `DrawGlyphRun`) directly onto the GPU recorded command streams.
- **State Stack**: Manages a local transform matrix, opacity, and clipping geometry stack, resolving struct mutation writebacks for commands.
- **Showcase Preview**: Created an interactive page (`WpfShowcasePage.cs`) showing grid lines, StreamGeometry stars, transforms, opacities, and formatted text blocks.

### 9. GPU-Accelerated Path Geometry Operations (Layer 3)
- **GPGPU Solver**: Implemented 100% GPU-bound analytical path boolean solvers (Union, Intersect, Difference, XOR, Reverse Difference) without CPU curve flattening or triangulation.
- **Anti-Artifacting & Precision Fixes**: Resolved chord intersections near endpoints using boundary perturbation, relaxed endpoint containment intervals, and dynamic 16-step subdivision for curved chords.

### 10. High-Performance GPU-Accelerated Image Effects Engine
- **Image Filters**: Built a custom compositor pipeline to execute GPU-bound effects (e.g. Blur, Grayscale, Invert, Sepia, Brightness, Contrast) via customized shaders.
- **Demo Gallery**: Added an interactive UI page enabling users to tweak filter properties live and see the rendering performance.

### 11. Instanced Snapping & Subpixel Positioned Text Rendering
- **Retina Snapping**: Implemented physical pixel boundary snapping for glyph positions, eliminating font-rendering blurriness and improving readability at standard/retina DPI levels.

### 12. ShaderToy Playground & Live Compilation Engine
- **WebGPU WGSL Engine**: Created a custom compositor drawing extension `ShaderToyExtensionPipeline` (registered under `IDrawingExtension.ShaderToy = 11`) to execute custom fragment shaders backed by WebGPU.
- **ShaderToy Compatibility**: Created `ShaderToyControl` simulating the standard ShaderToy environment, supporting standard input uniforms (resolution `iResolution`, time `iTime`, frames `iFrame`, and mouse position `iMouse`).
- **Interactive Editor UI**: Built `ShaderToyPlaygroundPage` consisting of a live WebGPU canvas, Preset dropdown loaded with rich graphics presets (Cosmic Waves, Star Nest, Raymarched Torus SDF), real-time Timeline control, monospaced text editor, and a live compiler log diagnostics console.
- **Panic Protection**: Subscribed to static `WgpuContext.OnWebGpuError` events to catch shader compilation errors safely and skip pipeline creation when errors occur, preventing underlying WebGPU/Rust panics from aborting the host process.
- **Headless Testing**: Implemented headless unit tests to verify the ShaderToy Playground page and compilation pipeline under testing environments.

---

## Commit Log

1. **`cb10f7c`** `backend: Implement deferred native resource disposal queue and finalizer safety`
2. **`81160dc`** `backend: Implement precise direction-aware winding intersection rules and pipeline cache optimizations`
3. **`f52bd12`** `scene: Add render commands for clipping, opacity masking, blend modes, and fix boundary crossings in Hatch shader`
4. **`0e5f6b6`** `compositor: Implement blend mode stack, geometry clipping masks, opacity masks, and offscreen texture caching`
5. **`3b860ea`** `text: Implement glyph outline caching and coordinate flipping along Y-axis`
6. **`cb67354`** `vector: Implement SVG elliptical arc path segment parsing and GPU compiler support`
7. **`3c424e2`** `cleanup: Remove redundant finalizers from compute accelerator and glyph atlas`
8. **`7d13f40`** `ui: Refactor ProGpuHostControl and fix WinUI Control template override`
9. **`64f724a`** `shim: Implement SkiaSharp compatibility API and drawing context integration`
10. **`3bff12c`** `vector: Implement GPU-accelerated analytical Path Boolean operations`
11. **`a51c0e0`** `backend: Fix Path Ops rendering artifacts via boundary perturbation, endpoint relaxation, and dynamic chord steps`
12. **`e84bea6`** `Improve text rendering performance and quality via instanced snapping and subpixel positioning`
13. **`8cd692a`** `Implement high performance GPU-accelerated Image Effects Engine and demo page`
14. **`fd7df1c`** `Implement System.Drawing.Common (GDI+) shim library using ProGPU drawing context backend`
15. **`90bac10`** `samples: Add GDI Shim Showcase Page`
16. **`a6f1c8e`** `samples: Add Glyph Run Showcase Page`
17. **`4ae7378`** `backend: Fix shaders DPI scaling for text snapping and glyph rendering`
18. **`3f575b6`** `shim: Implement PresentationCore WPF DrawingContext compatibility shim`
19. **`da76db2`** `samples: Add WPF DrawingContext Shim Showcase Page and register in MainWindow`
20. **`626bad1`** `tests: Add headless unit test verifying WPF Showcase Page rendering`
21. **`3c53832`** `docs: Update PR Summary with GDI+ and WPF DrawingContext shim details`
22. **`94cb793`** `Implement ShaderToyControl WinUI control for real-time WebGPU shader execution`
23. **`cc7861b`** `Add Text property to RichEditBox for raw string getting and setting`
24. **`d7cc550`** `Implement ShaderToyPlaygroundPage gallery page with editor, preset selection, and error console`
25. **`b0b0374`** `Integrate ShaderToy Playground page inside navigation drawer`
26. **`a259b2e`** `Relocate ShaderToyControl to ProGPU.Samples to resolve project circular dependencies`
27. **`66577ab`** `Fix StackPanel ambiguity, ComboBox SelectedIndex, and Border properties in ShaderToyPlaygroundPage.cs`
28. **`abd4e7f`** `Add headless test verifying ShaderToy Playground page rendering`
29. **`1f44bcf`** `Expose WebGPU compiler/validation error event and deferred render pipeline disposal helpers`
30. **`b499186`** `Implement and register ShaderToyExtensionPipeline compositor backend for fragment shader rendering`
31. **`441a735`** `Remove IsTabStop from ShaderToyControl to fix FrameworkElement compilation`
32. **`5cd061c`** `Fix StackOverflowException by removing redundant animation propagation loop in ShaderToyPlaygroundPage`
33. **`5534c20`** `Protect pipeline creation from compilation failures to prevent wgpu abort panics, and fix Preset 1 swizzling style`
