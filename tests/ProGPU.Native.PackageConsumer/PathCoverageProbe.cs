using System.Numerics;
using System.Runtime.InteropServices;
using ProGPU.Backend;
using ProGPU.Backend.Native;
using Silk.NET.WebGPU;

internal enum PathProbeApi { AutomaticLayout, NativeLayout, NativeSubmission, NativeRelease }

// Failure isolation only: this executes the packaged canonical kernel, not a
// replacement renderer. A passing probe never qualifies a failed native frame.
internal static unsafe class PathCoverageProbe
{
    internal static void RunNative(WgpuContext context)
    {
        using var renderer = new NativeCompositor(context, TextureFormat.Rgba8Unorm);
        using var target = new GpuTexture(context, 64, 16, TextureFormat.Rgba8Unorm,
            TextureUsage.RenderAttachment | TextureUsage.CopySrc, "Cold native path diagnostic");
        NativePathSegment[] segments = [
            new(NativePathSegmentKind.Line, new(8, 4), new(40, 4)),
            new(NativePathSegmentKind.Line, new(40, 4), new(40, 12)),
            new(NativePathSegmentKind.Line, new(40, 12), new(8, 12)),
            new(NativePathSegmentKind.Line, new(8, 12), new(8, 4))];
        var metrics = renderer.RenderPaths(target, 1,
            [new NativePathFill(0, 4, new(8, 4), new(40, 12), Vector4.One,
                Matrix3x2.Identity, NativeFillRule.NonZero, 8)],
            segments, new Vector4(0, 0, 0, 1), capturePayloadHash: true);
        Console.WriteLine($"package-consumer: native path prepared payload=" +
            $"{metrics.PayloadHash:X16}; vertices={metrics.VertexCount}; indices={metrics.IndexCount}; " +
            $"brushBytes={metrics.BrushUploadBytes}; pathBytes={metrics.PathUploadBytes}; " +
            $"atlas={metrics.AtlasWidth}x{metrics.AtlasHeight}; submissions={metrics.SubmissionCount}");
        renderer.WaitForSubmission(renderer.GetLastSubmissionToken());
        byte[] pixels = target.ReadPixels();
        int inside = (8 * 64 + 16) * 4, outside = (2 * 64 + 2) * 4;
        Console.WriteLine($"package-consumer: cold direct native path inside=" +
            $"({pixels[inside]},{pixels[inside + 1]},{pixels[inside + 2]},{pixels[inside + 3]}), " +
            $"outside=({pixels[outside]},{pixels[outside + 1]},{pixels[outside + 2]},{pixels[outside + 3]}); " +
            $"draws={metrics.DrawCallCount}; coverageBytes={metrics.CoverageStagingBytes}; " +
            $"backend={context.AdapterBackendType}; adapter={context.AdapterName}");
        if (pixels[inside] != 255 || pixels[inside + 1] != 255 || pixels[inside + 2] != 255 || pixels[inside + 3] != 255 ||
            pixels[outside] != 0 || pixels[outside + 1] != 0 || pixels[outside + 2] != 0 || pixels[outside + 3] != 255)
            throw new InvalidOperationException("Cold direct native path probe failed.");
    }

    internal static void Run(WgpuContext context, bool drawAtlas = false,
        bool sameSubmission = false, bool nativeLayout = false,
        PathProbeApi apiMode = PathProbeApi.AutomaticLayout)
    {
        const uint rowBytes = 256, height = 16;
        uint width = nativeLayout ? 40u : 64u;
        uint atlasX = nativeLayout ? 2u : 17u, atlasY = nativeLayout ? 2u : 19u;
        using var cache = new RenderPipelineCache(context);
        using var uniforms = new ProbeBuffer(context, 48, BufferUsage.Storage | BufferUsage.CopyDst);
        using var records = new ProbeBuffer(context, 32, BufferUsage.Storage | BufferUsage.CopyDst);
        using var segments = new ProbeBuffer(context, 4 * 48, BufferUsage.Storage | BufferUsage.CopyDst);
        using var coverage = new ProbeBuffer(context, rowBytes * height, BufferUsage.Storage | BufferUsage.CopySrc);
        using var combine = new ProbeBuffer(context, 40, BufferUsage.Storage | BufferUsage.CopyDst);
        using var atlas = new GpuTexture(context, 1024, 1024, TextureFormat.R8Unorm,
            TextureUsage.TextureBinding | TextureUsage.CopyDst | TextureUsage.CopySrc,
            "Cold partial path atlas diagnostic");
        // Exact PathRasterizerCommon.wgsl wire records. The rectangle interior
        // must be 255, its exterior 0 at the unchanged eight-by-eight sample grid.
        uniforms.Write<uint>([Bits(nativeLayout ? 4 : 0), 0, Bits(1), Bits(1), 0, 0, rowBytes / 4, width, height, 8, 0, 0]);
        records.Write<uint>([0, 4, Bits(8), Bits(4), Bits(40), Bits(12), 1, 0]);
        segments.Write<Segment>([
            new(new(8, 4), new(40, 4)), new(new(40, 4), new(40, 12)),
            new(new(40, 12), new(8, 12)), new(new(8, 12), new(8, 4))]);
        combine.Write<uint>(new uint[10]);
        uint[] bufferSizes = [uniforms.Size, records.Size, segments.Size, coverage.Size, combine.Size];
        var bufferPointers = stackalloc Silk.NET.WebGPU.Buffer*[5];
        bufferPointers[0] = uniforms.BufferPtr; bufferPointers[1] = records.BufferPtr;
        bufferPointers[2] = segments.BufferPtr; bufferPointers[3] = coverage.BufferPtr;
        bufferPointers[4] = combine.BufferPtr;
        BindGroupLayout* layout = null;
        PipelineLayout* pipelineLayout = null;
        BindGroup* group = null;
        CommandEncoder* encoder = null;
        CommandBuffer* command = null;
        try
        {
            var layoutEntries = stackalloc BindGroupLayoutEntry[5];
            var entries = stackalloc BindGroupEntry[5];
            for (int i = 0; i < 5; i++)
            {
                layoutEntries[i] = new BindGroupLayoutEntry {
                    Binding = (uint)i, Visibility = ShaderStage.Compute,
                    Buffer = new BufferBindingLayout {
                        Type = i == 3 ? BufferBindingType.Storage : BufferBindingType.ReadOnlyStorage,
                        MinBindingSize = i == 3 ? 4u : bufferSizes[i]
                    }
                };
                entries[i] = new BindGroupEntry {
                    Binding = (uint)i, Buffer = bufferPointers[i], Size = bufferSizes[i]
                };
            }
            var layoutDescriptor = new BindGroupLayoutDescriptor { EntryCount = 5, Entries = layoutEntries };
            layout = context.Api.DeviceCreateBindGroupLayout(context.Device, &layoutDescriptor);
            if (layout == null) throw new InvalidOperationException("Coverage probe layout rejected.");
            var pipelineLayoutDescriptor = new PipelineLayoutDescriptor { BindGroupLayoutCount = 1, BindGroupLayouts = &layout };
            pipelineLayout = context.Api.DeviceCreatePipelineLayout(context.Device, &pipelineLayoutDescriptor);
            if (pipelineLayout == null) throw new InvalidOperationException("Coverage probe pipeline layout rejected.");
            var shader = cache.GetOrCreateShader("PackagePathCoverageProbe", Shaders.PathRasterizerShader);
            var pipeline = cache.GetOrCreateComputePipeline("PackagePathCoverageProbe", shader, "cs_main_ordinary", pipelineLayout);
            var groupDescriptor = new BindGroupDescriptor { Layout = layout, EntryCount = 5, Entries = entries };
            group = context.Api.DeviceCreateBindGroup(context.Device, &groupDescriptor);
            if (group == null) throw new InvalidOperationException("Coverage probe bindings rejected.");
            var encoderDescriptor = new CommandEncoderDescriptor();
            encoder = context.Api.DeviceCreateCommandEncoder(context.Device, &encoderDescriptor);
            if (encoder == null) throw new InvalidOperationException("Coverage probe encoder rejected.");
            var passDescriptor = new ComputePassDescriptor();
            var pass = context.Api.CommandEncoderBeginComputePass(encoder, &passDescriptor);
            if (pass == null) throw new InvalidOperationException("Coverage probe pass rejected.");
            context.Api.ComputePassEncoderSetPipeline(pass, pipeline);
            context.Api.ComputePassEncoderSetBindGroup(pass, 0, group, 0, null);
            context.Api.ComputePassEncoderDispatchWorkgroups(pass, 1, 1, 1);
            context.Api.ComputePassEncoderEnd(pass);
            context.Api.ComputePassEncoderRelease(pass);
            var source = new ImageCopyBuffer {
                Buffer = coverage.BufferPtr,
                Layout = new TextureDataLayout { BytesPerRow = rowBytes, RowsPerImage = height }
            };
            var destination = new ImageCopyTexture {
                Texture = atlas.TexturePtr, Origin = new Origin3D(atlasX, atlasY, 0), Aspect = TextureAspect.All
            };
            var extent = new Extent3D(width, height, 1);
            context.Api.CommandEncoderCopyBufferToTexture(encoder, &source, &destination, &extent);
            if (sameSubmission)
            {
                DrawAtlas(context, atlas, cache, encoder, nativeLayout, apiMode,
                    apiMode == PathProbeApi.NativeRelease ? () => {
                        context.Api.BindGroupRelease(group); group = null;
                        uniforms.Dispose(); records.Dispose(); segments.Dispose(); coverage.Dispose(); combine.Dispose();
                        Console.WriteLine("package-consumer: released all five raster buffers and bind group before native completion wait");
                    } : null);
            }
            else
            {
                var commandDescriptor = new CommandBufferDescriptor();
                command = context.Api.CommandEncoderFinish(encoder, &commandDescriptor);
                if (command == null) throw new InvalidOperationException("Coverage probe commands rejected.");
                context.Submit(1, &command);
            }
            Console.WriteLine("package-consumer: coverage probe submitted canonical raster and partial atlas copy");
            // Submit the first draw before either readback. The diagnostic must
            // not warm up or synchronize the atlas through a CPU read first.
            if (drawAtlas && !sameSubmission) DrawAtlas(context, atlas, cache);
            byte[] pixels = atlas.ReadPixels();
            if (apiMode == PathProbeApi.NativeRelease)
            {
                if (pixels[(atlasY + 8) * 1024 + atlasX + 16] != 255 ||
                    pixels[(atlasY + 2) * 1024 + atlasX + 2] != 0 || pixels[0] != 0)
                    throw new InvalidOperationException("Released raster resource atlas probe failed.");
                Console.WriteLine("package-consumer: released raster atlas interior/exterior/untouched samples passed");
                return;
            }
            byte[] raw = coverage.ReadBytes();
            byte rawInside = raw[8 * rowBytes + 16], rawOutside = raw[2 * rowBytes + 2];
            byte atlasInside = pixels[(atlasY + 8) * 1024 + atlasX + 16];
            byte atlasOutside = pixels[(atlasY + 2) * 1024 + atlasX + 2];
            Console.WriteLine($"package-consumer: coverage probe raw=({rawInside},{rawOutside}), " +
                $"atlas=({atlasInside},{atlasOutside}), untouched={pixels[0]}; " +
                $"backend={context.AdapterBackendType}; adapter={context.AdapterName}");
            if (rawInside != 255 || rawOutside != 0 || atlasInside != 255 || atlasOutside != 0 || pixels[0] != 0)
                throw new InvalidOperationException("Canonical path coverage/partial atlas transfer probe failed.");
        }
        finally
        {
            if (command != null) context.Api.CommandBufferRelease(command);
            if (encoder != null) context.Api.CommandEncoderRelease(encoder);
            if (group != null) context.Api.BindGroupRelease(group);
            if (pipelineLayout != null) context.Api.PipelineLayoutRelease(pipelineLayout);
            if (layout != null) context.Api.BindGroupLayoutRelease(layout);
        }
    }

    private static void DrawAtlas(WgpuContext context, GpuTexture atlas, RenderPipelineCache cache,
        CommandEncoder* sharedEncoder = null, bool nativeLayout = false,
        PathProbeApi apiMode = PathProbeApi.AutomaticLayout, Action? releaseRaster = null)
    {
        using var target = new GpuTexture(context, 64, 16, TextureFormat.Rgba8Unorm,
            TextureUsage.RenderAttachment | TextureUsage.CopySrc, "Canonical vector atlas diagnostic");
        using var uniforms = new GpuBuffer(context, 224, BufferUsage.Uniform | BufferUsage.CopyDst);
        using var brushes = new GpuBuffer(context, nativeLayout ? 65536u : 256u, BufferUsage.Storage | BufferUsage.CopyDst);
        using var stops = new GpuBuffer(context, 32, BufferUsage.Storage | BufferUsage.CopyDst);
        using var vertices = new GpuBuffer(context, nativeLayout ? 65536u : 4 * 56u, BufferUsage.Vertex | BufferUsage.CopyDst);
        using var indices = new GpuBuffer(context, nativeLayout ? 65536u : 6 * 4u, BufferUsage.Index | BufferUsage.CopyDst);
        // Existing Vector.wgsl wire layouts, identical to native gpu_uniforms,
        // vector_vertex and the solid-brush sentinel. No reduced shader entry.
        float[] frame = new float[56];
        frame[0] = 2f / 64; frame[5] = -2f / 16; frame[10] = -1;
        frame[12] = -1; frame[13] = 1; frame[15] = 1;
        for (int matrix = 1; matrix < 3; matrix++)
            for (int diagonal = 0; diagonal < 4; diagonal++) frame[matrix * 16 + diagonal * 5] = 1;
        frame[48] = 64; frame[49] = 16; frame[50] = 1;
        uniforms.Write<float>(frame);
        float[] brush = new float[nativeLayout ? 128 : 64]; brush[1] = 1;
        if (nativeLayout)
        {
            brush[65] = 1;
            brush[80] = brush[81] = brush[82] = brush[83] = 1;
        }
        brushes.Write<float>(brush);
        stops.Write<uint>(new uint[8]);
        float[] vertexData = nativeLayout ? [
             4,  0, 1,1,1,1,  2, 2, 1, 4, 0,1,0,4,
            44,  0, 1,1,1,1, 42, 2, 1,44, 0,1,0,4,
            44, 16, 1,1,1,1, 42,18, 1,44,16,1,0,4,
             4, 16, 1,1,1,1,  2,18, 1, 4,16,1,0,4] : [
             0,  0, 1,1,1,1, 17,19, 0,64,16,0,0,4,
            64,  0, 1,1,1,1, 81,19, 0,64,16,0,0,4,
            64, 16, 1,1,1,1, 81,35, 0,64,16,0,0,4,
             0, 16, 1,1,1,1, 17,35, 0,64,16,0,0,4];
        vertices.Write<float>(vertexData);
        uint[] indexData = [0,1,2,0,2,3];
        indices.Write<uint>(indexData);
        if (nativeLayout)
        {
            // Explicit scalar diagnostic oracle for the tiny immutable payload,
            // using the native metrics' FNV-1a byte order, not a renderer path.
            ulong hash = 14695981039346656037UL;
            foreach (byte value in MemoryMarshal.AsBytes(vertexData.AsSpan())) hash = unchecked((hash ^ value) * 1099511628211UL);
            foreach (byte value in MemoryMarshal.AsBytes(indexData.AsSpan())) hash = unchecked((hash ^ value) * 1099511628211UL);
            foreach (byte value in MemoryMarshal.AsBytes(brush.AsSpan())) hash = unchecked((hash ^ value) * 1099511628211UL);
            Console.WriteLine($"package-consumer: reference native-layout payload={hash:X16}");
        }
        var attributes = stackalloc VertexAttribute[8];
        VertexFormat[] formats = [VertexFormat.Float32x2, VertexFormat.Float32x4,
            VertexFormat.Float32x2, VertexFormat.Float32, VertexFormat.Float32x2,
            VertexFormat.Float32, VertexFormat.Float32, VertexFormat.Float32];
        ulong[] offsets = [0,8,24,32,36,44,48,52];
        for (int i = 0; i < 8; i++) attributes[i] = new() {
            Format = formats[i], Offset = offsets[i], ShaderLocation = (uint)i };
        VertexBufferLayout[] vertexLayouts = [new() {
            ArrayStride = 56, StepMode = VertexStepMode.Vertex, AttributeCount = 8, Attributes = attributes }];
        var shader = cache.GetOrCreateShader("PackageCanonicalVectorProbe", Shaders.VectorShader);
        BindGroupLayout* uniformLayout = null; BindGroupLayout* atlasLayout = null;
        PipelineLayout* pipelineLayout = null;
        BindGroup* uniformGroup = null; BindGroup* atlasGroup = null;
        Sampler* sampler = null; CommandEncoder* encoder = null; CommandBuffer* command = null;
        try
        {
            if (apiMode != PathProbeApi.AutomaticLayout)
            {
                // Mirror the original native common vector layout, including
                // visibility/minimum sizes, without changing the shader ABI.
                var layoutEntries = stackalloc BindGroupLayoutEntry[3];
                for (int i = 0; i < 3; i++) layoutEntries[i] = new() {
                    Binding = (uint)i,
                    Visibility = i == 2 ? ShaderStage.Fragment : ShaderStage.Vertex | ShaderStage.Fragment,
                    Buffer = new BufferBindingLayout {
                        Type = i == 0 ? BufferBindingType.Uniform : BufferBindingType.ReadOnlyStorage,
                        MinBindingSize = i == 0 ? 224u : i == 1 ? 256u : 32u } };
                var layoutDescriptor = new BindGroupLayoutDescriptor { EntryCount = 3, Entries = layoutEntries };
                uniformLayout = context.Api.DeviceCreateBindGroupLayout(context.Device, &layoutDescriptor);
                var textureEntries = stackalloc BindGroupLayoutEntry[2];
                textureEntries[0] = new() { Binding = 0, Visibility = ShaderStage.Fragment,
                    Sampler = new SamplerBindingLayout { Type = SamplerBindingType.Filtering } };
                textureEntries[1] = new() { Binding = 1, Visibility = ShaderStage.Fragment,
                    Texture = new TextureBindingLayout { SampleType = TextureSampleType.Float,
                        ViewDimension = TextureViewDimension.Dimension2D } };
                var textureDescriptor = new BindGroupLayoutDescriptor { EntryCount = 2, Entries = textureEntries };
                atlasLayout = context.Api.DeviceCreateBindGroupLayout(context.Device, &textureDescriptor);
                if (uniformLayout == null || atlasLayout == null)
                    throw new InvalidOperationException("Native-layout probe layouts rejected.");
                var layouts = stackalloc BindGroupLayout*[2];
                layouts[0] = uniformLayout; layouts[1] = atlasLayout;
                var descriptor = new PipelineLayoutDescriptor { BindGroupLayoutCount = 2, BindGroupLayouts = layouts };
                pipelineLayout = context.Api.DeviceCreatePipelineLayout(context.Device, &descriptor);
                if (pipelineLayout == null) throw new InvalidOperationException("Native-layout probe pipeline layout rejected.");
            }
            var pipeline = cache.GetOrCreateRenderPipeline("PackageCanonicalVectorProbe", shader,
                fragmentEntry: "fs_main_unmasked", targetFormat: TextureFormat.Rgba8Unorm,
                vertexBufferLayouts: vertexLayouts, pipelineLayout: pipelineLayout);
            if (pipelineLayout != null)
            {
                // Native pipeline creation drops its caller layout reference here.
                context.Api.PipelineLayoutRelease(pipelineLayout);
                pipelineLayout = null;
            }
            if (apiMode == PathProbeApi.AutomaticLayout)
            {
                uniformLayout = context.Api.RenderPipelineGetBindGroupLayout(pipeline, 0);
                atlasLayout = context.Api.RenderPipelineGetBindGroupLayout(pipeline, 1);
            }
            var uniformEntries = stackalloc BindGroupEntry[3];
            uniformEntries[0] = new() { Binding = 0, Buffer = uniforms.BufferPtr, Size = 224 };
            uniformEntries[1] = new() { Binding = 1, Buffer = brushes.BufferPtr, Size = brushes.Size };
            uniformEntries[2] = new() { Binding = 2, Buffer = stops.BufferPtr, Size = 32 };
            var uniformDescriptor = new BindGroupDescriptor {
                Layout = uniformLayout, EntryCount = 3, Entries = uniformEntries };
            uniformGroup = context.Api.DeviceCreateBindGroup(context.Device, &uniformDescriptor);
            var samplerDescriptor = new SamplerDescriptor {
                AddressModeU = AddressMode.ClampToEdge, AddressModeV = AddressMode.ClampToEdge,
                AddressModeW = AddressMode.ClampToEdge, MinFilter = FilterMode.Linear,
                MagFilter = FilterMode.Linear, MipmapFilter = MipmapFilterMode.Nearest, MaxAnisotropy = 1 };
            sampler = context.Api.DeviceCreateSampler(context.Device, &samplerDescriptor);
            var atlasEntries = stackalloc BindGroupEntry[2];
            atlasEntries[0] = new() { Binding = 0, Sampler = sampler };
            atlasEntries[1] = new() { Binding = 1, TextureView = atlas.ViewPtr };
            var atlasDescriptor = new BindGroupDescriptor { Layout = atlasLayout, EntryCount = 2, Entries = atlasEntries };
            atlasGroup = context.Api.DeviceCreateBindGroup(context.Device, &atlasDescriptor);
            if (uniformGroup == null || atlasGroup == null || sampler == null)
                throw new InvalidOperationException("Canonical vector probe bindings rejected.");
            var encoderDescriptor = new CommandEncoderDescriptor();
            encoder = sharedEncoder == null
                ? context.Api.DeviceCreateCommandEncoder(context.Device, &encoderDescriptor)
                : sharedEncoder;
            var attachment = new RenderPassColorAttachment {
                View = target.ViewPtr, LoadOp = LoadOp.Clear, StoreOp = StoreOp.Store,
                ClearValue = new Color(0,0,0,1) };
            var passDescriptor = new RenderPassDescriptor { ColorAttachmentCount = 1, ColorAttachments = &attachment };
            var pass = context.Api.CommandEncoderBeginRenderPass(encoder, &passDescriptor);
            if (pass == null) throw new InvalidOperationException("Canonical vector probe pass rejected.");
            context.Api.RenderPassEncoderSetPipeline(pass, pipeline);
            context.Api.RenderPassEncoderSetBindGroup(pass, 0, uniformGroup, 0, null);
            context.Api.RenderPassEncoderSetBindGroup(pass, 1, atlasGroup, 0, null);
            context.Api.RenderPassEncoderSetVertexBuffer(pass, 0, vertices.BufferPtr, 0, 4 * 56);
            context.Api.RenderPassEncoderSetIndexBuffer(pass, indices.BufferPtr, IndexFormat.Uint32, 0, 6 * 4);
            context.Api.RenderPassEncoderDrawIndexed(pass, 6, 1, 0, 0, 0);
            context.Api.RenderPassEncoderEnd(pass);
            context.Api.RenderPassEncoderRelease(pass);
            var commandDescriptor = new CommandBufferDescriptor();
            command = context.Api.CommandEncoderFinish(encoder, &commandDescriptor);
            if (command == null) throw new InvalidOperationException("Canonical vector probe commands rejected.");
            if (apiMode == PathProbeApi.NativeSubmission || apiMode == PathProbeApi.NativeRelease)
            {
                // Existing wgpu-native extension ABI used by the C++ engine,
                // followed by the same exact-token wait as the native probe.
                var submit = (delegate* unmanaged[Cdecl]<Queue*, nuint, CommandBuffer**, ulong>)
                    context.Wgpu.Context.GetProcAddress("wgpuQueueSubmitForIndex");
                var poll = (delegate* unmanaged[Cdecl]<Device*, uint, void*, uint>)
                    context.Wgpu.Context.GetProcAddress("wgpuDevicePoll");
                var token = new SubmissionToken { Queue = context.Queue, Index = submit(context.Queue, 1, &command) };
                if (apiMode == PathProbeApi.NativeRelease)
                {
                    context.Api.CommandBufferRelease(command); command = null;
                    releaseRaster!();
                }
                Console.WriteLine($"package-consumer: native submission token={token.Index}; poll={poll(context.Device, 1, &token)}");
            }
            else
            {
                context.Submit(1, &command);
            }
            byte[] pixels = target.ReadPixels();
            int inside = (8 * 64 + 16) * 4, outside = (2 * 64 + 2) * 4;
            Console.WriteLine($"package-consumer: canonical vector atlas inside=" +
                $"({pixels[inside]},{pixels[inside+1]},{pixels[inside+2]},{pixels[inside+3]}), " +
                $"outside=({pixels[outside]},{pixels[outside+1]},{pixels[outside+2]},{pixels[outside+3]}); " +
                $"sameSubmission={sharedEncoder != null}; nativeLayout={nativeLayout}; api={apiMode}; " +
                $"backend={context.AdapterBackendType}; adapter={context.AdapterName}");
            if (pixels[inside] != 255 || pixels[inside+1] != 255 || pixels[inside+2] != 255 || pixels[inside+3] != 255 ||
                pixels[outside] != 0 || pixels[outside+1] != 0 || pixels[outside+2] != 0 || pixels[outside+3] != 255)
                throw new InvalidOperationException("Canonical vector atlas draw failed.");
        }
        finally
        {
            if (command != null) context.Api.CommandBufferRelease(command);
            if (encoder != null && sharedEncoder == null) context.Api.CommandEncoderRelease(encoder);
            if (atlasGroup != null) context.Api.BindGroupRelease(atlasGroup);
            if (uniformGroup != null) context.Api.BindGroupRelease(uniformGroup);
            if (sampler != null) context.Api.SamplerRelease(sampler);
            if (pipelineLayout != null) context.Api.PipelineLayoutRelease(pipelineLayout);
            if (atlasLayout != null) context.Api.BindGroupLayoutRelease(atlasLayout);
            if (uniformLayout != null) context.Api.BindGroupLayoutRelease(uniformLayout);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SubmissionToken
    {
        public Queue* Queue;
        public ulong Index;
    }

    // Own exactly one raw caller reference, like path_raster_resources in C++.
    // GpuBuffer.Dispose defers release and therefore cannot test this boundary.
    private sealed class ProbeBuffer : IDisposable
    {
        private readonly WgpuContext _context;
        public Silk.NET.WebGPU.Buffer* BufferPtr { get; private set; }
        public uint Size { get; }

        public ProbeBuffer(WgpuContext context, uint size, BufferUsage usage)
        {
            _context = context; Size = size;
            var descriptor = new BufferDescriptor { Size = size, Usage = usage };
            BufferPtr = context.Api.DeviceCreateBuffer(context.Device, &descriptor);
            if (BufferPtr == null) throw new InvalidOperationException("Probe buffer allocation failed.");
        }

        public void Write<T>(ReadOnlySpan<T> values) where T : unmanaged
        {
            ObjectDisposedException.ThrowIf(BufferPtr == null, this);
            var bytes = MemoryMarshal.AsBytes(values);
            if ((uint)bytes.Length > Size || (bytes.Length & 3) != 0)
                throw new ArgumentException("Probe write must fit its aligned buffer.", nameof(values));
            fixed (byte* data = bytes)
                _context.Api.QueueWriteBuffer(_context.Queue, BufferPtr, 0, data, (nuint)bytes.Length);
        }

        public void Dispose()
        {
            if (BufferPtr == null) return;
            _context.Api.BufferRelease(BufferPtr);
            BufferPtr = null;
        }

        public byte[] ReadBytes()
        {
            ObjectDisposedException.ThrowIf(BufferPtr == null, this);
            using var copy = new GpuBuffer(_context, Size, BufferUsage.CopyDst | BufferUsage.CopySrc);
            var descriptor = new CommandEncoderDescriptor();
            var encoder = _context.Api.DeviceCreateCommandEncoder(_context.Device, &descriptor);
            if (encoder == null) throw new InvalidOperationException("Probe readback encoder rejected.");
            CommandBuffer* command = null;
            try
            {
                _context.Api.CommandEncoderCopyBufferToBuffer(encoder, BufferPtr, 0, copy.BufferPtr, 0, Size);
                var commands = new CommandBufferDescriptor();
                command = _context.Api.CommandEncoderFinish(encoder, &commands);
                if (command == null) throw new InvalidOperationException("Probe readback commands rejected.");
                _context.Submit(1, &command);
                return copy.ReadBytes();
            }
            finally
            {
                if (command != null) _context.Api.CommandBufferRelease(command);
                _context.Api.CommandEncoderRelease(encoder);
            }
        }
    }

    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    private readonly struct Segment(Vector2 start, Vector2 end)
    {
        public readonly Vector2 Start = start;
        public readonly Vector2 End = end;
    }
}
