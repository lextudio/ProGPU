using System.Numerics;
using System.Runtime.InteropServices;
using ProGPU.Backend;
using ProGPU.Vector;
using Silk.NET.WebGPU;
using Xunit;

namespace ProGPU.Tests;

public sealed class LinearWindingRasterPipelineTests
{
    private const uint Sentinel = 0x5a17c0de;
    public bool RequireFallbackAdapter { get; init; }
    public bool CompareGeneralEntry { get; init; }

    [Fact]
    public unsafe void SignedLinearCoverageMatchesIndependentRegionsAndRejectsLaterCurves()
    {
        List<GpuPathRecord> records = [];
        List<GpuPathSegment> segments = [];
        AddRectangle(1, 1, 7, 6, false, false);
        AddRectangle(4, 2, 10, 7, false, false);
        AddRectangle(1, 1, 10, 7, true, false);
        AddRectangle(1, 1, 7, 6, false, true);
        static bool A(double x, double y) => x >= 1 && x < 7 && y >= 1 && y < 6;
        static bool B(double x, double y) => x >= 4 && x < 10 && y >= 2 && y < 7;
        static bool C(double x, double y) => x >= 1 && x < 10 && y >= 1 && y < 7;
        static int Bit(bool value) => value ? 1 : 0;
        const uint Raw = 0x20000000, Op = 0x80000000;
        // These region expressions are an independent geometric oracle, not a
        // second interpreter for the shader's postfix stack.
        List<(uint[]? Tokens, Func<double, double, int> Value)> programs =
        [
            (null, (x, y) => Bit(A(x, y))),
            ([0, 1, Op | 3, Raw | 2, Op | 7], (x, y) => Bit(A(x, y) || B(x, y)) - Bit(C(x, y))),
            ([Raw, Raw | 1, Op | 7, Raw | 2, Op | 7], (x, y) => Bit(A(x, y)) + Bit(B(x, y)) - Bit(C(x, y))),
            ([Raw, Op | 8, Raw | 1, Op | 7], (x, y) => -Bit(A(x, y)) + Bit(B(x, y))),
            ([Raw, Raw | 1, Op | 1], (x, y) => Bit(A(x, y) && !B(x, y))),
            ([Raw, Raw | 1, Op | 2], (x, y) => Bit(A(x, y) && B(x, y))),
            ([Raw, Raw | 1, Op | 3], (x, y) => Bit(A(x, y) || B(x, y))),
            ([Raw, Raw | 1, Op | 4], (x, y) => Bit(A(x, y) != B(x, y))),
            ([Raw, Raw | 1, Op | 5], (x, y) => Bit(!A(x, y) && B(x, y))),
            ([0x40000000, Raw, Op | 7], (x, y) => Bit(A(x, y))),
            ([3], (_, _) => 0), // Doubled even-odd contour is empty as a predicate.
            ([Raw | 3, Raw | 2, Op | 7], (x, y) => 2 * Bit(A(x, y)) - Bit(C(x, y))),
            (Enumerable.Repeat(Raw, 16).Concat(Enumerable.Repeat(Op | 7, 15)).ToArray(), (x, y) => 16 * Bit(A(x, y)))
        ];
        List<PathUniforms> uniforms = [];
        List<Func<double, double, int>> expectedRegions = [];
        uint outputWords = 4;
        foreach (var program in programs)
        {
            uint programIndex = checked((uint)records.Count);
            if (program.Tokens is not null)
                foreach (uint token in program.Tokens) records.Add(new GpuPathRecord { StartSegment = token });
            foreach (uint grid in new uint[] { 1, 2, 4, 8 })
            foreach (bool shifted in new[] { false, true })
            foreach (bool scaled in new[] { false, true })
            foreach (uint width in new uint[] { 0, 1, 3, 4, 17 })
            {
                uint stride = (width + 3) / 4 + 3;
                uniforms.Add(new PathUniforms
                {
                    XStart = shifted ? 0.25f : 0, YStart = shifted ? 0.5f : 0,
                    ScaleX = scaled ? 1.25f : 1, ScaleY = scaled ? 0.75f : 1,
                    PathIndex = 0, PathIndexB = programIndex,
                    PathOpKind = program.Tokens is null ? 0 : 0xc0000000u | checked((uint)program.Tokens.Length),
                    Width = width, Height = 11, SampleGrid = grid,
                    OutputOffsetWords = outputWords, OutputRowWords = stride
                });
                expectedRegions.Add(program.Value);
                outputWords += stride * 11 + 4;
            }
        }
        Assert.Equal(1040, uniforms.Count);
        uint[] expected = Enumerable.Repeat(Sentinel, checked((int)outputWords)).ToArray();
        for (int index = 0; index < uniforms.Count; index++)
        {
            var item = uniforms[index];
            for (uint y = 0; y < item.Height; y++)
            for (uint word = 0; word < (item.Width + 3) / 4; word++)
            {
                uint packed = 0;
                for (uint lane = 0; lane < 4 && word * 4 + lane < item.Width; lane++)
                {
                    int covered = 0;
                    for (uint sy = 0; sy < item.SampleGrid; sy++)
                    for (uint sx = 0; sx < item.SampleGrid; sx++)
                    {
                        double x = (item.XStart + word * 4 + lane + (sx + 0.5) / item.SampleGrid) / item.ScaleX;
                        double py = (item.YStart + y + (sy + 0.5) / item.SampleGrid) / item.ScaleY;
                        if (expectedRegions[index](x, py) != 0) covered++;
                    }
                    uint coverage = checked((uint)Math.Round(covered * 255.0 / (item.SampleGrid * item.SampleGrid)));
                    packed |= coverage << checked((int)(lane * 8));
                }
                expected[item.OutputOffsetWords + y * item.OutputRowWords + word] = packed;
            }
        }
        using var context = new WgpuContext { ForceFallbackAdapter = RequireFallbackAdapter };
        context.Initialize(null);
        Console.WriteLine($"Linear winding device: provider={context.BackendKind}, backend={context.AdapterBackendType}, adapter={context.AdapterName}");
        using var cache = new RenderPipelineCache(context);
        var shader = cache.GetOrCreateShader("LinearWindingSourceContracts", Shaders.PathRasterizerShader);
        var pipeline = cache.GetOrCreateComputePipeline("LinearWinding", shader, "cs_main_linear_winding");
        Assert.Equal(expected, Run(pipeline));
        if (CompareGeneralEntry)
        {
            var generalPipeline = cache.GetOrCreateComputePipeline("GeneralSignedWinding", shader, "cs_main");
            Assert.Equal(expected, Run(generalPipeline));
            Console.WriteLine("General signed entry also passed all 1040 independent region cases.");
        }
        Assert.Contains(expected, word => word != Sentinel && word != 0);
        // A curve in the unused tail of the complete uploaded source also closes
        // this entry. No earlier valid request may publish even a zero byte.
        foreach (uint kind in new uint[] { 1, 2, 3, 4, 5, uint.MaxValue })
        {
            segments.Add(new GpuPathSegment { SegmentType = kind });
            Assert.All(Run(pipeline), word => Assert.Equal(Sentinel, word));
            segments.RemoveAt(segments.Count - 1);
        }
        Console.WriteLine("Linear winding: 1040 independent region cases and six whole-inventory rejection controls passed.");

        void AddRectangle(float left, float top, float right, float bottom, bool reverse, bool doubled)
        {
            Vector2[] points = reverse
                ? [new(left, top), new(left, bottom), new(right, bottom), new(right, top)]
                : [new(left, top), new(right, top), new(right, bottom), new(left, bottom)];
            uint start = checked((uint)segments.Count);
            for (int copy = 0; copy < (doubled ? 2 : 1); copy++)
                for (int edge = 0; edge < 4; edge++)
                    segments.Add(new GpuPathSegment { P0 = points[edge], P1 = points[(edge + 1) % 4] });
            records.Add(new GpuPathRecord { StartSegment = start, SegmentCount = doubled ? 8u : 4u,
                MinX = left, MinY = top, MaxX = right, MaxY = bottom, FillRule = doubled ? 1u : 0u });
        }

        uint[] Run(ComputePipeline* selectedPipeline)
        {
            using var uniformBuffer = new GpuBuffer(context, checked((uint)(uniforms.Count * 48)), BufferUsage.Storage | BufferUsage.CopyDst);
            using var recordBuffer = new GpuBuffer(context, checked((uint)(records.Count * Marshal.SizeOf<GpuPathRecord>())), BufferUsage.Storage | BufferUsage.CopyDst);
            using var segmentBuffer = new GpuBuffer(context, checked((uint)(segments.Count * Marshal.SizeOf<GpuPathSegment>())), BufferUsage.Storage | BufferUsage.CopyDst);
            using var output = new GpuBuffer(context, outputWords * 4, BufferUsage.Storage | BufferUsage.CopyDst | BufferUsage.CopySrc);
            uniformBuffer.Write<PathUniforms>(CollectionsMarshal.AsSpan(uniforms));
            recordBuffer.Write<GpuPathRecord>(CollectionsMarshal.AsSpan(records));
            segmentBuffer.Write<GpuPathSegment>(CollectionsMarshal.AsSpan(segments));
            output.Write<uint>(Enumerable.Repeat(Sentinel, checked((int)outputWords)).ToArray());
            var layout = context.Api.ComputePipelineGetBindGroupLayout(selectedPipeline, 0);
            var entries = stackalloc BindGroupEntry[4];
            entries[0] = new() { Binding = 0, Buffer = uniformBuffer.BufferPtr, Size = uniformBuffer.Size };
            entries[1] = new() { Binding = 1, Buffer = recordBuffer.BufferPtr, Size = recordBuffer.Size };
            entries[2] = new() { Binding = 2, Buffer = segmentBuffer.BufferPtr, Size = segmentBuffer.Size };
            entries[3] = new() { Binding = 3, Buffer = output.BufferPtr, Size = output.Size };
            var descriptor = new BindGroupDescriptor { Layout = layout, EntryCount = 4, Entries = entries };
            var group = context.Api.DeviceCreateBindGroup(context.Device, &descriptor);
            CommandEncoder* encoder = null;
            CommandBuffer* commands = null;
            try
            {
                Assert.True(group != null);
                var encoderDescriptor = new CommandEncoderDescriptor();
                encoder = context.Api.DeviceCreateCommandEncoder(context.Device, &encoderDescriptor);
                var passDescriptor = new ComputePassDescriptor();
                var pass = context.Api.CommandEncoderBeginComputePass(encoder, &passDescriptor);
                context.Api.ComputePassEncoderSetPipeline(pass, selectedPipeline);
                context.Api.ComputePassEncoderSetBindGroup(pass, 0, group, 0, null);
                context.Api.ComputePassEncoderDispatchWorkgroups(pass, 2, 2, checked((uint)uniforms.Count));
                context.Api.ComputePassEncoderEnd(pass);
                context.Api.ComputePassEncoderRelease(pass);
                var commandDescriptor = new CommandBufferDescriptor();
                commands = context.Api.CommandEncoderFinish(encoder, &commandDescriptor);
                context.Submit(1, &commands);
                return MemoryMarshal.Cast<byte, uint>(output.ReadBytes()).ToArray();
            }
            finally
            {
                context.WaitIdle();
                if (commands != null) context.Api.CommandBufferRelease(commands);
                if (encoder != null) context.Api.CommandEncoderRelease(encoder);
                if (group != null) context.Api.BindGroupRelease(group);
                if (layout != null) context.Api.BindGroupLayoutRelease(layout);
            }
        }
    }
}
