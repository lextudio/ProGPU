using System.Reflection;
using ProGPU.Scene;
using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkCanvasImageOverloadCompatibilityTests
{
    [Theory]
    [InlineData(nameof(SKCanvas.DrawImage), 8)]
    [InlineData(nameof(SKCanvas.DrawBitmap), 8)]
    [InlineData(nameof(SKCanvas.DrawSurface), 4)]
    public void ImageFamiliesExposeNativeOptionalPaintOverloads(string methodName, int expectedCount)
    {
        var methods = typeof(SKCanvas)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == methodName)
            .ToArray();

        Assert.Equal(expectedCount, methods.Length);
        Assert.All(methods, method =>
        {
            var paint = Assert.Single(method.GetParameters(), parameter => parameter.Name == "paint");
            Assert.Equal(typeof(SKPaint), paint.ParameterType);
            Assert.True(paint.IsOptional);
            Assert.Null(paint.DefaultValue);
        });
    }

    [Fact]
    public void LegacyBitmapOverloadUsesNativeNearestSamplingByDefault()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 8f, 8f);
        using var bitmap = CreateRedBlueBitmap();

        canvas.DrawBitmap(bitmap, new SKRect(0f, 0f, 8f, 4f));

        var command = Assert.Single(
            context.Commands,
            command => command.Type == RenderCommandType.DrawTexture);
        Assert.Equal(RenderCommandType.DrawTexture, command.Type);
        Assert.Equal(TextureSamplingMode.Nearest, command.TextureSamplingMode);
    }

    [Fact]
    public void DrawBitmapUploadsDirectlyAndRetainsCallTimePixels()
    {
        using var bitmap = CreateRedBlueBitmap();
        using var surface = SKSurface.Create(
            new SKImageInfo(4, 2, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var paint = new SKPaint { IsAntialias = false };
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawBitmap(
            bitmap,
            new SKRect(0f, 0f, 2f, 1f),
            new SKRect(0f, 0f, 4f, 2f),
            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None),
            paint);

        bitmap.SetPixel(0, 0, SKColors.Green);
        surface.Flush();

        using var image = surface.Snapshot();
        var pixels = image.Texture.ReadPixels();
        AssertPixel(pixels, 4, 0, 0, SKColors.Red);
        AssertPixel(pixels, 4, 1, 1, SKColors.Red);
        AssertPixel(pixels, 4, 2, 0, SKColors.Blue);
        AssertPixel(pixels, 4, 3, 1, SKColors.Blue);
    }

    [Fact]
    public void DrawBitmapMipmapSamplingBuildsRetainedMipChain()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 8f, 8f);
        using var bitmap = new SKBitmap(
            new SKImageInfo(8, 8, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Red);

        canvas.DrawBitmap(
            bitmap,
            new SKRect(0f, 0f, 8f, 8f),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        var command = Assert.Single(
            context.Commands,
            command => command.Type == RenderCommandType.DrawTexture);
        Assert.Equal(TextureSamplingMode.LinearMipmap, command.TextureSamplingMode);
        Assert.Equal(4u, command.Texture!.MipLevelCount);
    }

    [Fact]
    public void DrawSurfaceFlushesAndRetainsSourceAtCallTime()
    {
        using var source = SKSurface.Create(
            new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var destination = SKSurface.Create(
            new SKImageInfo(4, 4, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var paint = new SKPaint { IsAntialias = false };
        source.Canvas.Clear(SKColors.Green);
        destination.Canvas.Clear(SKColors.Transparent);

        destination.Canvas.DrawSurface(source, 1f, 1f, paint);
        source.Canvas.Clear(SKColors.Red);
        destination.Flush();

        using var image = destination.Snapshot();
        var pixels = image.Texture.ReadPixels();
        AssertPixel(pixels, 4, 1, 1, SKColors.Green);
        AssertPixel(pixels, 4, 2, 2, SKColors.Green);
        Assert.Equal(0, ReadAlpha(pixels, 4, 0, 0));
    }

    [Fact]
    public void DrawBitmapRejectsNullAndIgnoresEmptyBitmap()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 8f, 8f);
        using var empty = new SKBitmap();

        canvas.DrawBitmap(empty, 0f, 0f);

        Assert.Empty(context.Commands);
        Assert.Throws<ArgumentNullException>(() => canvas.DrawBitmap(null!, 0f, 0f));
        Assert.Throws<ArgumentNullException>(() => canvas.DrawImage(null!, 0f, 0f));
        Assert.Throws<ArgumentNullException>(() => canvas.DrawSurface(null!, 0f, 0f));
    }

    private static SKBitmap CreateRedBlueBitmap()
    {
        var bitmap = new SKBitmap(
            new SKImageInfo(2, 1, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.SetPixel(0, 0, SKColors.Red);
        bitmap.SetPixel(1, 0, SKColors.Blue);
        return bitmap;
    }

    private static byte ReadAlpha(byte[] pixels, int width, int x, int y) =>
        pixels[(y * width + x) * 4 + 3];

    private static void AssertPixel(byte[] pixels, int width, int x, int y, SKColor expected)
    {
        var offset = (y * width + x) * 4;
        Assert.Equal(expected.Red, pixels[offset]);
        Assert.Equal(expected.Green, pixels[offset + 1]);
        Assert.Equal(expected.Blue, pixels[offset + 2]);
        Assert.Equal(expected.Alpha, pixels[offset + 3]);
    }
}
