using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ProGPU.Backend;
using ProGPU.Scene;
using ProGPU.Samples.Suntrail.Presentation;
using ProGPU.Samples.Suntrail.Rendering;
using ProGPU.Tests.Headless;
using Silk.NET.WebGPU;
using Windows.Devices.Input;
using Xunit;
using Key = Silk.NET.Input.Key;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class WorldPassUiTests : IDisposable
{
    private readonly Application _app = Application.Current;
    private readonly ElementTheme _theme = ThemeManager.CurrentTheme;
    private readonly WindowInputState _input = InputSystem.Current;

    public WorldPassUiTests() => Application.Current = new App();
    public void Dispose()
    {
        InputSystem.Current = _input; Application.Current = _app; ThemeManager.CurrentTheme = _theme;
    }

    [Theory]
    [InlineData(TextureFormat.Rgba8Unorm)]
    [InlineData(TextureFormat.Bgra8Unorm)]
    public unsafe void MenuTouchAndResizePreserveCompositedPixels(TextureFormat format)
    {
        using var context = new WgpuContext(); context.Initialize(null);
        using var compositor = new Compositor(context, format, new() { PrimarySampleCount = 4 });
        var pipeline = (ProceduralPipeline)compositor.RegisterDrawingExtension(ProceduralDrawingContextExtensions.Definition);
        pipeline.EnableMaterialPages = pipeline.EnableSharedInstances = pipeline.EnableSceneInstances = true;
        var view = new GameView(); view.SetSafeArea(new Thickness(59, 0, 59, 21));
        InputSystem.Current = InputSystem.CreateExternalState(view);
        var errors = new List<string>(); void Error(ErrorType type, string message) => errors.Add(message);
        WgpuContext.OnWebGpuError += Error;
        try
        {
            foreach (var (width, height, dpi) in new[] { (932u, 430u, 3), (844u, 390u, 2), (932u, 430u, 3) })
            {
                using var target = new GpuTexture(context, width * (uint)dpi, height * (uint)dpi, format,
                    TextureUsage.RenderAttachment | TextureUsage.CopySrc, "World/UI resize comparison", alphaMode: GpuTextureAlphaMode.Premultiplied);
                void Layout()
                {
                    for (int i = 0; i < 3; i++)
                    {
                        view.Measure(new(width, height)); view.Arrange(new Rect(0, 0, width, height));
                        view.UpdateAnimations(0);
                    }
                }
                void Render() => compositor.RenderScene(view, width, height, target.Width, target.Height, dpi, target.ViewPtr);
                void Compare(string state)
                {
                    Layout(); pipeline.EnableWorldPass = false;
                    for (int i = 0; i < 96; i++) { Render(); if (pipeline.MaterialFallbackPages == 0) break; }
                    Assert.Equal(0, pipeline.MaterialFallbackPages);
                    byte[] expected = target.ReadPixels();
                    pipeline.EnableWorldPass = true; Render(); Assert.True(pipeline.WorldPassReady);
                    AssertPixels(expected, target.ReadPixels(), $"{format}-{width}-{dpi}-{state}", target.Width, target.Height);
                    long uploads = pipeline.UploadedBytes, renders = pipeline.WorldRenderCount;
                    Render(); Assert.Equal(uploads, pipeline.UploadedBytes); Assert.Equal(renders, pipeline.WorldRenderCount);
                }
                Compare("menu");
                view.OnKeyDown(new() { Key = Key.Enter }); Layout(); Compare("playing");
                var stick = Descendants(view).OfType<TouchStick>().Single();
                var start = Vector2.Transform(new(76, 80), stick.GetGlobalCoordinateTransformMatrix());
                void Touch(PointerInputKind kind, Vector2 point) => InputSystem.InjectPointer(new(kind, 71,
                    PointerDeviceType.Touch, point, 1_000_000, IsInContact: kind != PointerInputKind.Canceled));
                Touch(PointerInputKind.Pressed, start); Layout(); Render();
                byte[] centered = target.ReadPixels(); long worldRenders = pipeline.WorldRenderCount;
                Touch(PointerInputKind.Moved, start + new Vector2(48, -18));
                // Repaint only the control: advancing animations would change the
                // game and hide a retained-world invalidation regression.
                view.Measure(new(width, height)); view.Arrange(new Rect(0, 0, width, height)); Render();
                Assert.True(stick.Axis > .5f); Assert.Equal(worldRenders, pipeline.WorldRenderCount);
                Assert.False(centered.AsSpan().SequenceEqual(target.ReadPixels())); Compare("dragging");
                Touch(PointerInputKind.Canceled, start); view.Deactivate(); Compare("paused");
                var settings = Descendants(view).OfType<Button>().Single(b => b.Content is TextBlock t && t.Text == "Settings");
                settings.OnKeyDown(new() { Key = Key.Enter }); Compare("settings");
                view.OnKeyDown(new() { Key = Key.Escape });
            }
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }
        finally { WgpuContext.OnWebGpuError -= Error; }
    }

    [Fact]
    public unsafe void RetainedUiRebuildsResourcesOnANewGpuDevice()
    {
        var view = new GameView(); view.OnKeyDown(new() { Key = Key.Enter });
        view.Measure(new(844, 390)); view.Arrange(new Rect(0, 0, 844, 390)); view.UpdateAnimations(0);
        byte[]? expected = null;
        for (int device = 0; device < 2; device++)
        {
            using var context = new WgpuContext(); context.Initialize(null);
            using var compositor = new Compositor(context, TextureFormat.Rgba8Unorm, new() { PrimarySampleCount = 4 });
            var pipeline = (ProceduralPipeline)compositor.RegisterDrawingExtension(ProceduralDrawingContextExtensions.Definition);
            pipeline.EnableMaterialPages = pipeline.EnableSharedInstances = pipeline.EnableSceneInstances = pipeline.EnableWorldPass = true;
            using var target = new GpuTexture(context, 1688, 780, TextureFormat.Rgba8Unorm,
                TextureUsage.RenderAttachment | TextureUsage.CopySrc, "Recreated game/UI device");
            for (int i = 0; i < 96; i++)
            {
                compositor.RenderScene(view, 844, 390, target.Width, target.Height, 2, target.ViewPtr);
                if (pipeline.MaterialFallbackPages == 0) break;
            }
            Assert.True(pipeline.WorldPassReady); Assert.True(pipeline.MaterialBakeCount > 0);
            Assert.Equal(0, pipeline.MaterialFallbackPages);
            var pixels = target.ReadPixels();
            if (expected is not null) AssertPixels(expected, pixels, "recreated-device", target.Width, target.Height);
            expected = pixels;
        }
    }

    private static IEnumerable<FrameworkElement> Descendants(FrameworkElement root)
    {
        yield return root;
        foreach (var child in root.Children.OfType<FrameworkElement>())
            foreach (var item in Descendants(child)) yield return item;
    }

    private static void AssertPixels(byte[] expected, byte[] actual, string name, uint width, uint height)
    {
        double sum = 0; int large = 0;
        for (int i = 0; i < actual.Length; i++)
        { int delta = Math.Abs(actual[i] - expected[i]); sum += delta; if (delta > 2) large++; }
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "artifacts"))) root = root.Parent;
        string folder = Path.Combine(root?.FullName ?? throw new InvalidOperationException("Artifacts root missing."), "artifacts/suntrail/world-pass-ui");
        Directory.CreateDirectory(folder);
        PngEncoder.SavePng(Path.Combine(folder, name + "-reference.png"), expected, width, height);
        PngEncoder.SavePng(Path.Combine(folder, name + "-depth.png"), actual, width, height);
        string report = FormattableString.Invariant($"{name}: mean={sum / actual.Length:F6} over2={large}/{actual.Length}");
        File.AppendAllText(Path.Combine(folder, "quality.txt"), report + "\n");
        Assert.True(sum / actual.Length < .15, report); Assert.True(large < actual.Length / 1000, report);
    }
}
