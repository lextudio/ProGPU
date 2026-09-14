using System.Numerics;
using System.Runtime.InteropServices;
using ProGPU.Backend;
using ProGPU.GameEngine.Rendering;
using ProGPU.Scene;
using ProGPU.Samples.Suntrail.Presentation;
using ProGPU.Samples.Suntrail.Rendering;
using Silk.NET.WebGPU;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SharedInstanceTests
{
    [Fact]
    public void ShaderRecordsHaveDocumentedByteOffsets()
    {
        var source = new MaterialSourceInstance(new(1, 2, 3, 4), new(5, 6, 7, 8), new(9, 10, 11, 12), new(13, 14, 15, 16));
        var sourceBytes = MemoryMarshal.AsBytes(new[] { source }.AsSpan());
        Assert.Equal(64, sourceBytes.Length);
        Assert.Equal(Enumerable.Range(1, 16).Select(i => (float)i).ToArray(), MemoryMarshal.Cast<byte, float>(sourceBytes).ToArray());
        var page = new MaterialPageReference(new(1, 2, 3, 4), new(5, 6, 7, 8), 2047, 1);
        var bytes = MemoryMarshal.AsBytes(new[] { page }.AsSpan());
        Assert.Equal(48, bytes.Length);
        Assert.Equal(new uint[] { 2047, 1, 0, 0 }, MemoryMarshal.Cast<byte, uint>(bytes[32..]).ToArray());
    }

    [Fact]
    public void RetainedUpdatesOwnTheirShadowAndWriteOneEnclosingChangedRange()
    {
        using var context = new WgpuContext(); context.Initialize(null);
        using var stream = new RetainedGpuBuffer<Vector4>(context, 8, BufferUsage.Storage | BufferUsage.CopySrc, "Retained update regression");
        Vector4[] records = [new(1), new(2), new(3), new(4), new(5)];
        Assert.True(stream.Update(records)); Assert.Equal(80, stream.UploadedBytes);
        Assert.False(stream.Update(records)); Assert.Equal(1, stream.WriteCount);
        records[1] = new(7); records[3] = new(9);
        Assert.True(stream.Update(records)); Assert.Equal(128, stream.UploadedBytes); Assert.Equal(2, stream.WriteCount);
        Assert.Equal(MemoryMarshal.AsBytes(records.AsSpan()).ToArray(), stream.Buffer.ReadBytes(0, 80));
        Assert.True(stream.Update(records.AsSpan(0, 2))); Assert.Equal(128, stream.UploadedBytes);
        Assert.True(stream.Update(records)); Assert.Equal(176, stream.UploadedBytes);
        uint generation = stream.Generation;
        Assert.True(stream.Update(ReadOnlySpan<Vector4>.Empty)); Assert.Equal(generation + 1, stream.Generation);
        Assert.Equal(176, stream.UploadedBytes); Assert.Equal(0, stream.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Update(new Vector4[9]));
        Assert.Equal(0, stream.Count);
        stream.Dispose(); Assert.Throws<ObjectDisposedException>(() => stream.Update(records));
    }

    [Fact]
    public void BitwiseUpdatesPreserveSignedZeroAndNaNPayloadsWithoutReplayAllocation()
    {
        using var context = new WgpuContext(); context.Initialize(null);
        using var stream = new RetainedGpuBuffer<Vector4>(context, 1, BufferUsage.Storage | BufferUsage.CopySrc, "Bitwise update regression");
        Vector4[] records = [default]; stream.Update(records);
        records[0].X = BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
        Assert.True(stream.Update(records));
        records[0].Y = BitConverter.Int32BitsToSingle(unchecked((int)0x7fc00001));
        Assert.True(stream.Update(records)); Assert.False(stream.Update(records));
        records[0].Y = BitConverter.Int32BitsToSingle(unchecked((int)0x7fc00002));
        Assert.True(stream.Update(records));
        Assert.Equal(MemoryMarshal.AsBytes(records.AsSpan()).ToArray(), stream.Buffer.ReadBytes());
        for (int i = 0; i < 100; i++) stream.Update(records);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) stream.Update(records);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Theory]
    [InlineData(0, 1, 1)] [InlineData(0, 3, 4)]
    [InlineData(1, 1, 4)] [InlineData(2, 1, 4)] [InlineData(3, 1, 4)]
    [InlineData(4, 1, 4)] [InlineData(5, 1, 4)] [InlineData(6, 1, 4)]
    [InlineData(7, 1, 1)] [InlineData(7, 3, 4)]
    public unsafe void SharedSourcesPreservePixelsForPainterAndDepthModes(int world, int dpi, int samples)
    {
        using var context = new WgpuContext(); context.Initialize(null);
        using var compositor = new Compositor(context, TextureFormat.Rgba8Unorm, new() { PrimarySampleCount = (uint)samples });
        var pipeline = (ProceduralPipeline)compositor.RegisterDrawingExtension(ProceduralDrawingContextExtensions.Definition);
        pipeline.EnableMaterialPages = true;
        const uint width = 932, height = 430;
        using var target = new GpuTexture(context, width * (uint)dpi, height * (uint)dpi, TextureFormat.Rgba8Unorm,
            TextureUsage.RenderAttachment | TextureUsage.CopySrc, "Shared source comparison");
        var view = new GameSurface(); view.Session.StartLevel(world);
        view.Measure(new(width, height)); view.Arrange(new Rect(0, 0, width, height));
        for (int i = 0; i < 120; i++) view.Session.Step(default);
        void Render() => compositor.RenderScene(view, width, height, target.Width, target.Height, dpi, target.ViewPtr);
        var errors = new List<string>(); void Error(ErrorType type, string message) => errors.Add(message);
        WgpuContext.OnWebGpuError += Error;
        try
        {
            foreach (bool worldPass in new[] { false, true })
            {
                pipeline.EnableWorldPass = worldPass;
                // Include changed time/actor data between comparisons, then settle
                // residency before comparing the transport of that exact same frame.
                view.Session.Step(default); view.Batch.Build(view.Session, new(width, height), view.Session.Time); view.Invalidate();
                pipeline.EnableSharedInstances = false;
                for (int i = 0; i < 96; i++) { Render(); if (pipeline.MaterialFallbackPages == 0) break; }
                Assert.Equal(0, pipeline.MaterialFallbackPages); var expanded = target.ReadPixels();
                pipeline.EnableSharedInstances = true; Render();
                Assert.Equal(expanded, target.ReadPixels());
                long uploaded = pipeline.UploadedBytes, calls = pipeline.SharedUploadCalls;
                Render(); Render(); Assert.Equal(uploaded, pipeline.UploadedBytes); Assert.Equal(calls, pipeline.SharedUploadCalls);
                pipeline.EnableSharedInstances = false; Render(); Assert.Equal(expanded, target.ReadPixels());
            }
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }
        finally { WgpuContext.OnWebGpuError -= Error; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public unsafe void ColdPagesAndRecreatedDevicesUseTheSameFallbackAndPixels(bool worldPass)
    {
        (byte[] Pixels, int Fallback, long Bakes) Capture(bool shared)
        {
            using var context = new WgpuContext(); context.Initialize(null);
            using var compositor = new Compositor(context, TextureFormat.Rgba8Unorm);
            var pipeline = (ProceduralPipeline)compositor.RegisterDrawingExtension(ProceduralDrawingContextExtensions.Definition);
            pipeline.EnableMaterialPages = true; pipeline.EnableSharedInstances = shared; pipeline.EnableWorldPass = worldPass;
            using var target = new GpuTexture(context, 932, 430, TextureFormat.Rgba8Unorm,
                TextureUsage.RenderAttachment | TextureUsage.CopySrc, "Cold shared source comparison");
            var view = new GameSurface(); view.Session.StartLevel(0);
            view.Measure(new(932, 430)); view.Arrange(new Rect(0, 0, 932, 430));
            view.Batch.Build(view.Session, new(932, 430), 0); view.Invalidate();
            compositor.RenderScene(view, 932, 430, 932, 430, 1, target.ViewPtr);
            return (target.ReadPixels(), pipeline.MaterialFallbackPages, pipeline.MaterialBakeCount);
        }
        var expanded = Capture(false); var shared = Capture(true); var recreated = Capture(true);
        Assert.True(expanded.Fallback > 0); Assert.Equal(32, expanded.Bakes);
        Assert.Equal(expanded.Fallback, shared.Fallback); Assert.Equal(expanded.Bakes, shared.Bakes);
        Assert.Equal(expanded.Pixels, shared.Pixels); Assert.Equal(shared.Pixels, recreated.Pixels);
    }
}
