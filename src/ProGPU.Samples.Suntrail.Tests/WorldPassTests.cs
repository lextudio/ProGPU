using ProGPU.Backend;
using ProGPU.GameEngine.Rendering;
using ProGPU.Scene;
using ProGPU.Samples.Suntrail.Game;
using ProGPU.Samples.Suntrail.Presentation;
using ProGPU.Samples.Suntrail.Rendering;
using ProGPU.Tests.Headless;
using Silk.NET.WebGPU;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class WorldPassTests
{
    [Fact]
    public void TargetBudgetCountsResolveMsaaAndDepthWithoutAllocating()
    {
        Assert.Equal(2796L * 1290 * 36, SceneRenderTarget.EstimateBytes(2796, 1290, 4));
        Assert.Equal(2796L * 1290 * 8, SceneRenderTarget.EstimateBytes(2796, 1290, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneRenderTarget.EstimateBytes(100, 100, 2));
        Assert.Throws<OverflowException>(() => SceneRenderTarget.EstimateBytes(uint.MaxValue, uint.MaxValue, 4));
    }

    [Fact]
    public void TargetsReuseStableAttachmentsAndRefuseOversizedResolution()
    {
        using var context = new WgpuContext(); context.Initialize(null);
        using var scene = new SceneRenderTarget(context, 1_048_576);
        Assert.True(scene.TryEnsure(64, 32, 4, TextureFormat.Rgba8Unorm));
        var color = scene.Color; var depth = scene.Depth; uint generation = scene.Generation;
        Assert.True(scene.TryEnsure(64, 32, 4, TextureFormat.Rgba8Unorm));
        Assert.Same(color, scene.Color); Assert.Same(depth, scene.Depth); Assert.Equal(generation, scene.Generation);
        Assert.True(scene.TryEnsure(80, 32, 1, TextureFormat.Rgba8Unorm));
        Assert.NotSame(color, scene.Color); Assert.Null(scene.MultisampleColor);
        Assert.Equal(generation + 1, scene.Generation);
        Assert.False(scene.TryEnsure(1024, 1024, 4, TextureFormat.Rgba8Unorm));
        Assert.Null(scene.Color); Assert.Null(scene.Depth); Assert.Equal(0, scene.ResidentBytes);
    }

    [Theory]
    [InlineData(0, 1, 1)] [InlineData(0, 3, 4)]
    [InlineData(1, 1, 4)] [InlineData(2, 1, 4)] [InlineData(3, 1, 4)]
    [InlineData(4, 1, 4)] [InlineData(5, 1, 4)] [InlineData(6, 1, 4)]
    [InlineData(7, 1, 1)] [InlineData(7, 3, 4)]
    public unsafe void OpaqueDepthPreservesPainterOutputAndUnchangedSceneReplays(int world, int dpi, int samples)
    {
        using var context = new WgpuContext(); context.Initialize(null);
        using var compositor = new Compositor(context, TextureFormat.Rgba8Unorm, new() { PrimarySampleCount = (uint)samples });
        var pipeline = (ProceduralPipeline)compositor.RegisterDrawingExtension(ProceduralDrawingContextExtensions.Definition);
        pipeline.EnableMaterialPages = true;
        const uint width = 932, height = 430;
        using var target = new GpuTexture(context, width * (uint)dpi, height * (uint)dpi, TextureFormat.Rgba8Unorm,
            TextureUsage.RenderAttachment | TextureUsage.CopySrc, "World depth comparison");
        var view = new GameSurface(); view.Session.StartLevel(world);
        view.Measure(new(width, height)); view.Arrange(new Rect(0, 0, width, height));
        for (int i = 0; i < 120; i++) view.Session.Step(default);
        view.Batch.Build(view.Session, new(width, height), view.Session.Time); view.Invalidate();
        void Render() => compositor.RenderScene(view, width, height, target.Width, target.Height, dpi, target.ViewPtr);
        var errors = new List<string>(); void Error(ErrorType type, string message) => errors.Add(message);
        WgpuContext.OnWebGpuError += Error;
        try
        {
            for (int i = 0; i < 96; i++) { Render(); if (pipeline.MaterialFallbackPages == 0) break; }
            Assert.Equal(0, pipeline.MaterialFallbackPages);
            var painter = target.ReadPixels();
            pipeline.EnableWorldPass = true; Render(); context.WaitIdle();
            Assert.True(pipeline.WorldPassReady); Assert.Equal(1, pipeline.WorldRenderCount);
            var depth = target.ReadPixels();
            string folder = ArtifactFolder();
            PngEncoder.SavePng(Path.Combine(folder, $"world-{world}-dpi{dpi}-msaa{samples}-painter.png"), painter, target.Width, target.Height);
            PngEncoder.SavePng(Path.Combine(folder, $"world-{world}-dpi{dpi}-msaa{samples}-depth.png"), depth, target.Width, target.Height);
            double sum = 0; int large = 0;
            for (int i = 0; i < depth.Length; i++) { int delta = Math.Abs(depth[i] - painter[i]); sum += delta; if (delta > 2) large++; }
            string report = $"world={world} dpi={dpi} samples={samples} mean={sum / depth.Length:F6} over2={large}/{depth.Length} targetBytes={pipeline.WorldResidentBytes}";
            File.AppendAllText(Path.Combine(folder, "quality.txt"), report + "\n");
            Assert.True(sum / depth.Length < .15, report);
            Assert.True(large < depth.Length / 1000, report);
            long uploads = pipeline.UploadedBytes, renders = pipeline.WorldRenderCount;
            Render(); Render(); Assert.Equal(uploads, pipeline.UploadedBytes); Assert.Equal(renders, pipeline.WorldRenderCount);
            Assert.Equal(depth, target.ReadPixels());
            view.Session.Step(default); view.Batch.Build(view.Session, new(width, height), view.Session.Time); view.Invalidate(); Render();
            Assert.Equal(renders + 1, pipeline.WorldRenderCount);
            pipeline.EnableWorldPass = false; Render(); Assert.False(pipeline.WorldPassReady); Assert.Equal(0, pipeline.WorldResidentBytes);
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }
        finally { WgpuContext.OnWebGpuError -= Error; }
    }

    private static string ArtifactFolder()
    {
        var path = new DirectoryInfo(AppContext.BaseDirectory);
        while (path is not null && !Directory.Exists(Path.Combine(path.FullName, "artifacts"))) path = path.Parent;
        string folder = Path.Combine(path?.FullName ?? throw new InvalidOperationException("Repository artifact directory missing."), "artifacts/suntrail/world-pass");
        Directory.CreateDirectory(folder); return folder;
    }
}
