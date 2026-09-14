using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProGPU.Backend;
using ProGPU.GameEngine.Assets;
using ProGPU.Scene;
using ProGPU.Samples.Suntrail.Rendering;
using Silk.NET.WebGPU;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class RasterArtworkTests
{
    [Theory]
    [InlineData(TextureFormat.Rgba8Unorm, 1, 64, 64)]
    [InlineData(TextureFormat.Rgba8Unorm, 4, 150, 110)]
    [InlineData(TextureFormat.Bgra8Unorm, 4, 150, 110)]
    public unsafe void NineSliceCornersEdgesAndPartialTilesMatchIndependentPixelReference(TextureFormat format, int samples, int width, int height)
    {
        var source = new byte[96 * 96 * 4];
        for (int y = 0; y < 96; y++)
            for (int x = 0; x < 96; x++)
            {
                int p = (y * 96 + x) * 4;
                source[p] = (byte)(x * 2); source[p + 1] = (byte)(y * 2); source[p + 2] = 80; source[p + 3] = 255;
            }
        var image = RasterImage.FromRgba(96, 96, source); var view = new BatchView();
        view.Set([new(image, new(0, 0, width, height), new(0, 0, 96, 96)) { NineSlice = new(32, 32, 1, 1) }]);
        using var context = new WgpuContext(); context.Initialize(null);
        using var compositor = new Compositor(context, format, new() { PrimarySampleCount = (uint)samples });
        var pipeline = (RasterArtworkPipeline)compositor.RegisterDrawingExtension(RasterArtworkDrawing.Definition);
        using var target = new GpuTexture(context, (uint)width, (uint)height, format, TextureUsage.RenderAttachment | TextureUsage.CopySrc, "Nine-slice reference");
        view.Measure(new(width, height)); view.Arrange(new(0, 0, width, height));
        void Render() => compositor.RenderScene(view, (uint)width, (uint)height, target.Width, target.Height, 1, target.ViewPtr);
        Render(); Assert.Equal(1L, pipeline.Draws);
        var pixels = target.ReadPixels(); int r = format == TextureFormat.Bgra8Unorm ? 2 : 0;
        // Build expected rows/columns by explicitly repeating each 32-pixel middle
        // strip, independently of the shader's continuous piecewise remapping.
        static int[] Coordinates(int size)
        {
            var result = new int[size];
            for (int i = 0; i < 32; i++) { result[i] = i; result[size - 32 + i] = 64 + i; }
            int tile = 32;
            while (tile < size - 32)
            {
                for (int i = 0; i < 32 && tile + i < size - 32; i++) result[tile + i] = 32 + i;
                tile += 32;
            }
            return result;
        }
        var xs = Coordinates(width); var ys = Coordinates(height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int p = (y * width + x) * 4;
                Assert.InRange((int)pixels[p + r], xs[x] * 2 - 1, xs[x] * 2 + 1);
                Assert.InRange((int)pixels[p + 1], ys[y] * 2 - 1, ys[y] * 2 + 1);
                Assert.Equal((byte)255, pixels[p + 3]);
            }
        long upload = pipeline.UploadedInstanceBytes; Render(); Assert.Equal(upload, pipeline.UploadedInstanceBytes);
        Assert.Equal(pixels, target.ReadPixels());
    }

    [Fact]
    public void NineSliceRejectsEmptyCentersInsufficientBordersAndNonfiniteScaling()
    {
        var image = RasterImage.FromRgba(3, 3, new byte[36]);
        foreach (var sizing in new[] { new Vector4(2, 1, 1, 1), new Vector4(1, 1, 0, 1), new Vector4(1, 1, float.NaN, 1) })
            Assert.Throws<ArgumentException>(() => new RasterArtworkBatch(new(), [new(image, new(0, 0, 3, 3), new(0, 0, 3, 3)) { NineSlice = sizing }]));
        Assert.Throws<ArgumentException>(() => new RasterArtworkBatch(new(), [new(image, new(0, 0, 1, 1), new(0, 0, 3, 3)) { NineSlice = Vector4.One }]));
    }

    private sealed class BatchView : Control
    {
        public RasterArtworkBatch? Batch;
        public void Set(ReadOnlySpan<RasterArtworkSprite> sprites) { Batch = new(this, sprites); Invalidate(); }
        public override void OnRender(DrawingContext context) { if (Batch is { } batch) context.DrawArtworkBatch(batch); }
    }

    [Fact]
    public void BatchOwnsItsInputAndValidatesEverySourceAndCombinedBounds()
    {
        var image = RasterImage.FromRgba(1, 1, [1, 2, 3, 255]);
        var sprites = new[] { new RasterArtworkSprite(image, new(-4, 3, 8, 9), new(0, 0, 1, 1)),
            new RasterArtworkSprite(image, new(10, -2, 2, 3), new(0, 0, 1, 1)) };
        object owner = new(); var batch = new RasterArtworkBatch(owner, sprites);
        sprites[0] = default;
        Assert.Same(owner, batch.Owner); Assert.Same(image, batch.Sprites[0].Image);
        Assert.Equal(new Rect(-4, -2, 16, 14), batch.Bounds);
        Assert.Throws<ArgumentException>(() => new RasterArtworkBatch(owner, [new(image, new(0, 0, 1, 1), new(0, 0, .5f, 1))]));
        Assert.Throws<ArgumentException>(() => new RasterArtworkBatch(owner, [new(image, new(float.MaxValue, 0, float.MaxValue, 1), new(0, 0, 1, 1))]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RasterArtworkBatch(owner, new RasterArtworkSprite[RasterArtworkBatch.MaximumSprites + 1]));
        Assert.True(new RasterArtworkBatch(owner, []).Sprites.IsEmpty);
    }

    [Theory]
    [InlineData(TextureFormat.Rgba8Unorm, 1)]
    [InlineData(TextureFormat.Bgra8Unorm, 4)]
    public unsafe void ManySpritesKeepPainterOrderBatchAdjacentImagesAndReuseUploads(TextureFormat format, int samples)
    {
        var red = RasterImage.FromRgba(1, 1, [255, 0, 0, 255]);
        var green = RasterImage.FromRgba(1, 1, [0, 255, 0, 255]);
        var view = new BatchView();
        using var context = new WgpuContext(); context.Initialize(null);
        using var compositor = new Compositor(context, format, new() { PrimarySampleCount = (uint)samples });
        compositor.ClearColor = Vector4.Zero;
        var pipeline = (RasterArtworkPipeline)compositor.RegisterDrawingExtension(RasterArtworkDrawing.Definition);
        using var target = new GpuTexture(context, 128, 64, format, TextureUsage.RenderAttachment | TextureUsage.CopySrc, "Artwork batch regression");
        view.Measure(new(128, 64)); view.Arrange(new(0, 0, 128, 64));
        void Render() => compositor.RenderScene(view, 128, 64, 128, 64, 1, target.ViewPtr);
        // Nonadjacent red runs must not be moved together across the green run.
        foreach (int count in new[] { 50, 150, 5 })
        {
            var sprites = new RasterArtworkSprite[count * 2 + 1];
            for (int i = 0; i < count; i++) sprites[i] = new(red, new(8, 8, 16, 16), new(0, 0, 1, 1));
            for (int i = count; i < count * 2; i++) sprites[i] = new(green, new(32, 8, 16, 16), new(0, 0, 1, 1));
            sprites[^1] = new(red, new(32, 8, 8, 8), new(0, 0, 1, 1)); view.Set(sprites);
            long beforeDraws = pipeline.Draws; Render(); Assert.Equal(3L, pipeline.Draws - beforeDraws);
            var pixels = target.ReadPixels(); int r = format == TextureFormat.Bgra8Unorm ? 2 : 0;
            int overlap = (10 * 128 + 34) * 4, uncovered = (14 * 128 + 44) * 4;
            Assert.Equal((byte)255, pixels[overlap + r]); Assert.Equal((byte)0, pixels[overlap + 1]);
            Assert.Equal((byte)255, pixels[uncovered + 1]); Assert.Equal((byte)0, pixels[uncovered + r]);
            long textureBytes = pipeline.UploadedPixelBytes, instanceBytes = pipeline.UploadedInstanceBytes, uniformBytes = pipeline.UploadedUniformBytes;
            Render(); Assert.Equal(pixels, target.ReadPixels());
            Assert.Equal(textureBytes, pipeline.UploadedPixelBytes); Assert.Equal(instanceBytes, pipeline.UploadedInstanceBytes);
            Assert.Equal(uniformBytes, pipeline.UploadedUniformBytes); Assert.Equal(8, pipeline.ResidentPixelBytes);
        }
        view.Set([]); Render(); Assert.Equal(0, pipeline.ResidentPixelBytes);
    }

    private sealed class View(RasterImage image) : Control
    {
        public override void OnRender(DrawingContext context) => context.DrawArtwork(new(this, image, new Rect(16, 16, 64, 32), new Rect(0, 0, 1, 1)));
    }

    [Theory]
    [InlineData(TextureFormat.Rgba8Unorm, 1)]
    [InlineData(TextureFormat.Rgba8Unorm, 4)]
    [InlineData(TextureFormat.Bgra8Unorm, 4)]
    public unsafe void SourceFrameClampingOpacityResizeReplayAndDeviceOwnership(TextureFormat format, int samples)
    {
        // Original red/green fixture: selecting the red frame must never sample green.
        var image = RasterImage.FromRgba(1, 2, [255, 0, 0, 255, 0, 255, 0, 255]);
        var view = new View(image) { Opacity = .5f };
        byte[]? reference = null;
        for (int device = 0; device < 2; device++)
        {
            using var context = new WgpuContext(); context.Initialize(null);
            using var compositor = new Compositor(context, format, new() { PrimarySampleCount = (uint)samples });
            compositor.ClearColor = Vector4.Zero;
            var pipeline = (RasterArtworkPipeline)compositor.RegisterDrawingExtension(RasterArtworkDrawing.Definition);
            var errors = new List<string>(); void Error(ErrorType type, string message) => errors.Add(message);
            WgpuContext.OnWebGpuError += Error;
            try
            {
                foreach (uint width in new[] { 128u, 192u, 128u })
                {
                    using var target = new GpuTexture(context, width * 2, 192, format, TextureUsage.RenderAttachment | TextureUsage.CopySrc, "Artwork regression");
                    view.Visibility = Visibility.Visible; view.Measure(new(width, 96)); view.Arrange(new Rect(0, 0, width, 96));
                    void Render() => compositor.RenderScene(view, width, 96, target.Width, target.Height, 2, target.ViewPtr);
                    Render(); var pixels = target.ReadPixels();
                    int offset = (24 * 2 * (int)target.Width + 32 * 2) * 4;
                    Assert.InRange(pixels[offset + (format == TextureFormat.Bgra8Unorm ? 2 : 0)], (byte)126, (byte)129);
                    Assert.Equal((byte)0, pixels[offset + 1]); Assert.InRange(pixels[offset + 3], (byte)126, (byte)129);
                    Assert.Equal((byte)0, pixels[3]); // Outside the logical quad remains clear.
                    long uploads = pipeline.UploadedPixelBytes, uniforms = pipeline.UploadedUniformBytes;
                    Render(); Assert.Equal(pixels, target.ReadPixels());
                    Assert.Equal(uploads, pipeline.UploadedPixelBytes); Assert.Equal(uniforms, pipeline.UploadedUniformBytes);
                    Assert.Equal(8, pipeline.ResidentPixelBytes);
                    if (width == 128)
                    { if (reference is not null) Assert.Equal(reference, pixels); reference = pixels; }
                    view.Visibility = Visibility.Collapsed; Render(); Assert.Equal(0, pipeline.ResidentPixelBytes);
                }
                Assert.Empty(errors);
            }
            finally { WgpuContext.OnWebGpuError -= Error; }
        }
    }
}
