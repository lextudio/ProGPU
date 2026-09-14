using System.Numerics;
using System.Runtime.InteropServices;
using ProGPU.Backend;
using ProGPU.Vector;
using Silk.NET.WebGPU;
using Xunit;

namespace ProGPU.Tests;

public sealed class GpuHitTestGeometryLaneTests
{
    [Theory]
    [InlineData("compare_segment_lanes")]
    [InlineData("compare_triangle_lanes")]
    public unsafe void FourGeometryLanesMatchOriginalGpuPredicates(string entryPoint)
    {
        const int caseCount = 512;
        // 96-byte WGSL record: a/b, four coordinate vectors, then four uint results.
        var data = new Vector4[caseCount * 6];
        var random = new Random(41973);
        float Coordinate() => random.Next(-1000, 1001) / 16f;
        for (int i = 0; i < caseCount; i++)
        {
            Vector2 a = new(Coordinate(), Coordinate());
            Vector2 b = new(Coordinate(), Coordinate());
            data[i * 6] = new(a.X, a.Y, b.X, b.Y);
            for (int vector = 1; vector <= 4; vector++)
                data[i * 6 + vector] = new(Coordinate(), Coordinate(), Coordinate(), Coordinate());
            // Parallel, collinear, zero-length, endpoint and near-threshold pairs;
            // independent randomized lanes above also cover crossings and misses.
            if (i % 4 != 0)
            {
                float slope = i % 4 == 1 ? 0 : i % 4 == 2 ? 0.0000005f : 0.000002f;
                data[i * 6] = new(0, 0, 1, 0);
                data[i * 6 + 1] = new(-1, 0, 1, 2);
                data[i * 6 + 2] = new(0, 0.00005f, 0.0002f, -slope);
                data[i * 6 + 3] = new(2, 0, 1, -1);
                data[i * 6 + 4] = data[i * 6 + 2] + new Vector4(slope);
                if (i % 8 == 3) data[i * 6] = Vector4.Zero;
                if (i % 8 == 7) data[i * 6] = new(1, 0, 0, 0);
            }
            if (entryPoint == "compare_triangle_lanes" && i % 4 != 0)
            {
                // Vertices, hypotenuse boundary, interior/exterior, both windings
                // and collinear triangles. Other cases retain randomized inputs.
                data[i * 6] = i % 8 < 4 ? new(0, 0, 2, 0) : new(2, 0, 0, 0);
                data[i * 6 + 1] = new(0, 1, 2, 2);
                data[i * 6 + 2] = new(0, 1, 0, 2);
                data[i * 6 + 3] = Vector4.Zero;
                data[i * 6 + 4] = new Vector4(i % 4 == 2 ? 0 : 2);
                if (i % 4 == 3) data[i * 6 + 2] = new(-0.000001f, 0.000001f, 0, 2);
            }
            data[i * 6 + 5] = new Vector4(-1); // every output lane must be written
        }

        using var context = new WgpuContext();
        context.Initialize(null);
        using var cache = new RenderPipelineCache(context);
        using var buffer = new GpuBuffer(context, checked((uint)(data.Length * 16)),
            BufferUsage.Storage | BufferUsage.CopyDst | BufferUsage.CopySrc);
        buffer.Write<Vector4>(data);
        string source = ShaderResource.Load(typeof(GpuHitTestEngine), "GpuHitTesting.wgsl") +
            "\n" + ShaderResource.Load(typeof(GpuHitTestGeometryLaneTests), "HitGeometryLanes.wgsl");
        var shader = cache.GetOrCreateShader("HitGeometryLanes", source);
        var pipeline = cache.GetOrCreateComputePipeline("HitGeometryLanes", shader, entryPoint);
        var layout = context.Api.ComputePipelineGetBindGroupLayout(pipeline, 0);
        var entry = new BindGroupEntry { Binding = 6, Buffer = buffer.BufferPtr, Size = buffer.Size };
        var bindDescriptor = new BindGroupDescriptor { Layout = layout, EntryCount = 1, Entries = &entry };
        var group = context.Api.DeviceCreateBindGroup(context.Device, &bindDescriptor);
        CommandEncoder* encoder = null;
        CommandBuffer* commands = null;
        try
        {
            Assert.True(group != null);
            var encoderDescriptor = new CommandEncoderDescriptor();
            encoder = context.Api.DeviceCreateCommandEncoder(context.Device, &encoderDescriptor);
            var passDescriptor = new ComputePassDescriptor();
            var pass = context.Api.CommandEncoderBeginComputePass(encoder, &passDescriptor);
            context.Api.ComputePassEncoderSetPipeline(pass, pipeline);
            context.Api.ComputePassEncoderSetBindGroup(pass, 0, group, 0, null);
            context.Api.ComputePassEncoderDispatchWorkgroups(pass, caseCount, 1, 1);
            context.Api.ComputePassEncoderEnd(pass);
            context.Api.ComputePassEncoderRelease(pass);
            var commandDescriptor = new CommandBufferDescriptor();
            commands = context.Api.CommandEncoderFinish(encoder, &commandDescriptor);
            context.Submit(1, &commands);
            var result = MemoryMarshal.Cast<byte, uint>(buffer.ReadBytes());
            int hits = 0;
            int misses = 0;
            for (int i = 0; i < caseCount; i++)
                for (int lane = 0; lane < 4; lane++)
                {
                    uint comparison = result[i * 24 + 20 + lane];
                    Assert.True(comparison is 0 or 3, $"{entryPoint} case {i}, lane {lane}: {comparison}");
                    if (comparison == 3) hits++; else misses++;
                }
            Assert.True(hits > 0 && misses > 0);
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
