using System;
using System.Numerics;
using Microsoft.UI.Xaml;
using ProGPU.Scene;
using ProGPU.Tests.Headless;
using ProGPU.Vector;
using Xunit;

namespace ProGPU.Tests;

public sealed class LayerRenderTests
{
    [Fact]
    public void CachedLayerCompositeIncludesVisualLocalTransform()
    {
        var window = HeadlessWindow.Shared;
        window.Resize(160, 100);
        window.Content = new LayerHostVisual();

        try
        {
            window.Render();

            var pixels = window.ReadPixels();
            var background = ReadPixel(pixels, window.Width, x: 10, y: 10);
            var rotatedOnly = ReadPixel(pixels, window.Width, x: 100, y: 25);
            var unrotatedOnly = ReadPixel(pixels, window.Width, x: 85, y: 40);

            AssertRed(rotatedOnly);
            AssertColorNear(background, unrotatedOnly, tolerance: 12);
        }
        finally
        {
            window.Content = null;
        }
    }

    private static RgbaPixel ReadPixel(byte[] pixels, uint width, int x, int y)
    {
        var index = ((y * (int)width) + x) * 4;
        return new RgbaPixel(
            pixels[index + 0],
            pixels[index + 1],
            pixels[index + 2],
            pixels[index + 3]);
    }

    private static void AssertRed(RgbaPixel pixel)
    {
        Assert.True(pixel.R >= 220, $"Expected cached layer to render red, found {pixel}.");
        Assert.True(pixel.G <= 35, $"Expected cached layer green channel to stay low, found {pixel}.");
        Assert.True(pixel.B <= 35, $"Expected cached layer blue channel to stay low, found {pixel}.");
        Assert.Equal(255, pixel.A);
    }

    private static void AssertColorNear(RgbaPixel expected, RgbaPixel actual, int tolerance)
    {
        Assert.InRange(Math.Abs(expected.R - actual.R), 0, tolerance);
        Assert.InRange(Math.Abs(expected.G - actual.G), 0, tolerance);
        Assert.InRange(Math.Abs(expected.B - actual.B), 0, tolerance);
        Assert.InRange(Math.Abs(expected.A - actual.A), 0, tolerance);
    }

    private readonly record struct RgbaPixel(byte R, byte G, byte B, byte A);

    private sealed class LayerHostVisual : FrameworkElement
    {
        private readonly RotatedCachedLayerVisual _layer = new();

        public LayerHostVisual()
        {
            Width = 160f;
            Height = 100f;
            AddChild(_layer);
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            _layer.Measure(new Vector2(40f, 20f));
            return availableSize;
        }

        protected override void ArrangeOverride(Rect arrangeRect)
        {
            _layer.Arrange(new Rect(80f, 30f, 40f, 20f));
        }
    }

    private sealed class RotatedCachedLayerVisual : FrameworkElement
    {
        public RotatedCachedLayerVisual()
        {
            Width = 40f;
            Height = 20f;
            Rotation = MathF.PI * 0.5f;
            CacheAsLayer = true;
        }

        public override void OnRender(DrawingContext context)
        {
            context.DrawRectangle(
                new SolidColorBrush(new Vector4(1f, 0f, 0f, 1f)),
                null,
                new Rect(0f, 0f, 40f, 20f));
        }
    }
}
