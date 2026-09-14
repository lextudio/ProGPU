using System.Numerics;
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

public sealed class SceneInstanceTests
{
    [Theory]
    [InlineData(0, 0, 1)] [InlineData(32760, -300, .5375f)] [InlineData(-40, 110, 3)]
    public void CameraProjectsWorldAndParallaxWithoutMutatingPlacement(float x, float y, float scale)
    {
        var camera = new Camera2D(new(x, y), scale);
        var bounds = new Vector4(32800, 600, 240, 310);
        foreach (var factor in new[] { Vector2.One, Vector2.Zero, new Vector2(.52f, .3f), new Vector2(.25f, 0), new Vector2(-.5f, 1.5f) })
        {
            var placement = SpritePlacement.World(bounds, factor);
            Assert.Equal(new Vector4((bounds.X - x * factor.X) * scale, (bounds.Y - y * factor.Y) * scale, bounds.Z * scale, bounds.W * scale), camera.ProjectBounds(placement));
            var restored = camera.Unproject(camera.Project(new(bounds.X, bounds.Y), factor), factor);
            Assert.InRange(Vector2.Distance(restored, new(bounds.X, bounds.Y)), 0, .008f);
            Assert.Equal(bounds, placement.Bounds);
        }
        Assert.Equal(bounds, camera.ProjectBounds(SpritePlacement.Screen(bounds)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Camera2D(default, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Camera2D(new(float.NaN, 0), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Camera2D(default, float.PositiveInfinity));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void RecordedPlacementsReconstructEveryVisibleSpriteDuringGameplay(int world)
    {
        var game = new GameSession(); game.StartLevel(world);
        var batch = new ProceduralBatch();
        for (int tick = 0; tick < 1800; tick++)
        {
            game.Step(RoutePilot.GetInput(game));
            if (tick % 60 != 0) continue;
            batch.Build(game, new(932, 430), game.Time);
            Assert.Equal(batch.Count, batch.Placements.Length);
            for (int i = 0; i < batch.Count; i++)
                Assert.Equal(batch.Sprites[i].Bounds, batch.Camera.ProjectBounds(batch.Placements[i]));
        }
    }

    [Fact]
    public unsafe void StaticSourcePrefixDoesNotUploadForAtmosphereAnimation()
    {
        using var context = new WgpuContext(); context.Initialize(null);
        using var compositor = new Compositor(context, TextureFormat.Rgba8Unorm);
        var pipeline = (ProceduralPipeline)compositor.RegisterDrawingExtension(ProceduralDrawingContextExtensions.Definition);
        pipeline.EnableMaterialPages = pipeline.EnableSharedInstances = pipeline.EnableSceneInstances = true;
        using var target = new GpuTexture(context, 932, 430, TextureFormat.Rgba8Unorm, TextureUsage.RenderAttachment, "Scene upload regression");
        var view = new GameSurface(); view.Session.StartLevel(0);
        view.Measure(new(932, 430)); view.Arrange(new Rect(0, 0, 932, 430));
        void Render(float time)
        {
            view.Batch.Build(view.Session, new(932, 430), time); view.Invalidate();
            compositor.RenderScene(view, 932, 430, 932, 430, 1, target.ViewPtr);
        }
        Render(.025f);
        int count = view.Batch.Count, staticCount = pipeline.StaticSourceInstances;
        Assert.InRange(staticCount, 1, count - 1);
        long uploaded = pipeline.SharedSourceUploadBytes;
        Render(.026f);
        Assert.Equal(count, view.Batch.Count); Assert.Equal(staticCount, pipeline.StaticSourceInstances);
        Assert.InRange(pipeline.SharedSourceUploadBytes - uploaded, 1, (count - staticCount) * 64L);
        uploaded = pipeline.SharedSourceUploadBytes;
        Render(.026f); Assert.Equal(uploaded, pipeline.SharedSourceUploadBytes);
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(0, 3)] [InlineData(1, 1)] [InlineData(2, 1)]
    [InlineData(3, 1)] [InlineData(4, 1)] [InlineData(5, 1)] [InlineData(6, 1)]
    [InlineData(7, 1)] [InlineData(7, 3)]
    public unsafe void GpuCameraAndPackedSourcesPreserveScrollingAndTransparentOrder(int world, int dpi)
    {
        using var context = new WgpuContext(); context.Initialize(null);
        using var compositor = new Compositor(context, TextureFormat.Rgba8Unorm, new() { PrimarySampleCount = 4 });
        var pipeline = (ProceduralPipeline)compositor.RegisterDrawingExtension(ProceduralDrawingContextExtensions.Definition);
        pipeline.EnableMaterialPages = pipeline.EnableSharedInstances = true;
        const uint width = 932, height = 430;
        using var target = new GpuTexture(context, width * (uint)dpi, height * (uint)dpi, TextureFormat.Rgba8Unorm,
            TextureUsage.RenderAttachment | TextureUsage.CopySrc, "Scene camera comparison");
        var view = new GameSurface(); view.Session.StartLevel(world);
        view.Measure(new(width, height)); view.Arrange(new Rect(0, 0, width, height));
        void Render() => compositor.RenderScene(view, width, height, target.Width, target.Height, dpi, target.ViewPtr);
        var errors = new List<string>(); void Error(ErrorType type, string message) => errors.Add(message);
        WgpuContext.OnWebGpuError += Error;
        try
        {
            for (int pose = 0; pose < 3; pose++)
            {
                for (int tick = 0; tick < 360; tick++) view.Session.Step(RoutePilot.GetInput(view.Session));
                view.Batch.Build(view.Session, new(width, height), view.Session.Time); view.Invalidate();
                foreach (bool depth in new[] { false, true })
                {
                    pipeline.EnableWorldPass = depth; pipeline.EnableSceneInstances = false;
                    for (int i = 0; i < 96; i++) { Render(); if (pipeline.MaterialFallbackPages == 0) break; }
                    Assert.Equal(0, pipeline.MaterialFallbackPages);
                    var reference = target.ReadPixels();
                    pipeline.EnableSceneInstances = true; Render(); var actual = target.ReadPixels();
                    double sum = 0; int over2 = 0;
                    for (int i = 0; i < actual.Length; i++) { int delta = Math.Abs(actual[i] - reference[i]); sum += delta; if (delta > 2) over2++; }
                    string report = $"world={world} dpi={dpi} pose={pose} depth={depth} mean={sum / actual.Length:F6} over2={over2}/{actual.Length}";
                    // CPU/GPU float contraction can differ in last-bit projection;
                    // keep a tight visible-pixel gate rather than masking those edges.
                    var folder = ArtifactFolder();
                    File.AppendAllText(Path.Combine(folder, "quality.txt"), report + "\n");
                    if (pose == 2)
                    {
                        string name = $"world-{world}-dpi{dpi}-depth{depth}";
                        PngEncoder.SavePng(Path.Combine(folder, name + "-reference.png"), reference, target.Width, target.Height);
                        PngEncoder.SavePng(Path.Combine(folder, name + "-scene.png"), actual, target.Width, target.Height);
                    }
                    Assert.True(sum / actual.Length < .15, report);
                    Assert.True(over2 < actual.Length / 1000, report);
                    long bytes = pipeline.UploadedBytes, renders = pipeline.WorldRenderCount;
                    Render(); Assert.Equal(bytes, pipeline.UploadedBytes); Assert.Equal(renders, pipeline.WorldRenderCount);
                    Assert.Equal(actual, target.ReadPixels());
                }
            }
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }
        finally { WgpuContext.OnWebGpuError -= Error; }
    }

    private static string ArtifactFolder()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "artifacts"))) root = root.Parent;
        string folder = Path.Combine(root?.FullName ?? throw new InvalidOperationException("Repository artifacts directory missing."), "artifacts/suntrail/scene-instances");
        Directory.CreateDirectory(folder); return folder;
    }
}
