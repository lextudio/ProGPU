using System.Numerics;
using System.Runtime.InteropServices;
using ProGPU.Backend;
using ProGPU.Backend.Native;
using Silk.NET.WebGPU;

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
            segments, new Vector4(0, 0, 0, 1));
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

    internal static void Run(WgpuContext context)
    {
        const uint rowBytes = 256, width = 64, height = 16;
        using var cache = new RenderPipelineCache(context);
        using var uniforms = new GpuBuffer(context, 48, BufferUsage.Storage | BufferUsage.CopyDst);
        using var records = new GpuBuffer(context, 32, BufferUsage.Storage | BufferUsage.CopyDst);
        using var segments = new GpuBuffer(context, 4 * 48, BufferUsage.Storage | BufferUsage.CopyDst);
        using var coverage = new GpuBuffer(context, rowBytes * height, BufferUsage.Storage | BufferUsage.CopySrc);
        using var combine = new GpuBuffer(context, 40, BufferUsage.Storage | BufferUsage.CopyDst);
        using var atlas = new GpuTexture(context, 1024, 1024, TextureFormat.R8Unorm,
            TextureUsage.TextureBinding | TextureUsage.CopyDst | TextureUsage.CopySrc,
            "Cold partial path atlas diagnostic");
        // Exact PathRasterizerCommon.wgsl wire records. The rectangle interior
        // must be 255, its exterior 0 at the unchanged eight-by-eight sample grid.
        uniforms.Write<uint>([0, 0, Bits(1), Bits(1), 0, 0, rowBytes / 4, width, height, 8, 0, 0]);
        records.Write<uint>([0, 4, Bits(8), Bits(4), Bits(40), Bits(12), 1, 0]);
        segments.Write<Segment>([
            new(new(8, 4), new(40, 4)), new(new(40, 4), new(40, 12)),
            new(new(40, 12), new(8, 12)), new(new(8, 12), new(8, 4))]);
        combine.Write<uint>(new uint[10]);
        GpuBuffer[] buffers = [uniforms, records, segments, coverage, combine];
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
                        MinBindingSize = i == 3 ? 4u : buffers[i].Size
                    }
                };
                entries[i] = new BindGroupEntry {
                    Binding = (uint)i, Buffer = buffers[i].BufferPtr, Size = buffers[i].Size
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
                Texture = atlas.TexturePtr, Origin = new Origin3D(17, 19, 0), Aspect = TextureAspect.All
            };
            var extent = new Extent3D(width, height, 1);
            context.Api.CommandEncoderCopyBufferToTexture(encoder, &source, &destination, &extent);
            var commandDescriptor = new CommandBufferDescriptor();
            command = context.Api.CommandEncoderFinish(encoder, &commandDescriptor);
            if (command == null) throw new InvalidOperationException("Coverage probe commands rejected.");
            context.Submit(1, &command);
            Console.WriteLine("package-consumer: coverage probe submitted canonical raster and partial atlas copy");
            byte[] raw = coverage.ReadBytes();
            byte[] pixels = atlas.ReadPixels();
            byte rawInside = raw[8 * rowBytes + 16], rawOutside = raw[2 * rowBytes + 2];
            byte atlasInside = pixels[(19 + 8) * 1024 + 17 + 16];
            byte atlasOutside = pixels[(19 + 2) * 1024 + 17 + 2];
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

    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    private readonly struct Segment(Vector2 start, Vector2 end)
    {
        public readonly Vector2 Start = start;
        public readonly Vector2 End = end;
    }
}
