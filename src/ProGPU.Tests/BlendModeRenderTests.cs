using System.Numerics;
using Microsoft.UI.Xaml;
using ProGPU.Backend;
using ProGPU.Scene;
using ProGPU.Tests.Headless;
using ProGPU.Vector;
using Xunit;

namespace ProGPU.Tests;

public sealed class BlendModeRenderTests
{
    [Fact]
    public void ScreenBlendPremultipliesStraightVectorSource()
    {
        var window = HeadlessWindow.Shared;
        window.Resize(32, 32);
        window.Content = new VectorBlendVisual(
            GpuBlendMode.Screen,
            background: new Vector4(0f, 0f, 0f, 1f),
            foreground: new Vector4(1f, 0f, 0f, 0.5f));

        try
        {
            window.Render();

            var pixel = ReadPixel(window.ReadPixels(), window.Width, x: 16, y: 16);

            Assert.InRange(pixel.R, 120, 136);
            Assert.InRange(pixel.G, 0, 8);
            Assert.InRange(pixel.B, 0, 8);
            Assert.Equal(255, pixel.A);
        }
        finally
        {
            window.Content = null;
        }
    }

    [Fact]
    public void MultiplyBlendPremultipliesStraightVectorSource()
    {
        var window = HeadlessWindow.Shared;
        window.Resize(32, 32);
        window.Content = new VectorBlendVisual(
            GpuBlendMode.Multiply,
            background: new Vector4(0.5f, 0.5f, 0.5f, 1f),
            foreground: new Vector4(1f, 0f, 0f, 0.5f));

        try
        {
            window.Render();

            var pixel = ReadPixel(window.ReadPixels(), window.Width, x: 16, y: 16);

            Assert.InRange(pixel.R, 120, 136);
            Assert.InRange(pixel.G, 58, 70);
            Assert.InRange(pixel.B, 58, 70);
            Assert.Equal(255, pixel.A);
        }
        finally
        {
            window.Content = null;
        }
    }

    [Fact]
    public void DstOverBlendPremultipliesStraightVectorSource()
    {
        var window = HeadlessWindow.Shared;
        window.Resize(32, 32);
        window.Content = new DstOverTransparentVisual();

        try
        {
            window.Render();

            var pixel = ReadPixel(window.ReadPixels(), window.Width, x: 16, y: 16);

            Assert.InRange(pixel.R, 120, 136);
            Assert.InRange(pixel.G, 0, 8);
            Assert.InRange(pixel.B, 0, 8);
            Assert.InRange(pixel.A, 120, 136);
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

    private readonly record struct RgbaPixel(byte R, byte G, byte B, byte A);

    private sealed class VectorBlendVisual : FrameworkElement
    {
        private readonly GpuBlendMode _blendMode;
        private readonly Vector4 _background;
        private readonly Vector4 _foreground;

        public VectorBlendVisual(GpuBlendMode blendMode, Vector4 background, Vector4 foreground)
        {
            _blendMode = blendMode;
            _background = background;
            _foreground = foreground;
            Width = 32f;
            Height = 32f;
        }

        public override void OnRender(DrawingContext context)
        {
            context.DrawRectangle(
                new SolidColorBrush(_background),
                null,
                new Rect(0f, 0f, 32f, 32f));
            context.PushBlendMode(_blendMode);
            context.DrawRectangle(
                new SolidColorBrush(_foreground),
                null,
                new Rect(0f, 0f, 32f, 32f));
            context.PopBlendMode();
        }
    }

    private sealed class DstOverTransparentVisual : FrameworkElement
    {
        public DstOverTransparentVisual()
        {
            Width = 32f;
            Height = 32f;
        }

        public override void OnRender(DrawingContext context)
        {
            context.PushBlendMode(GpuBlendMode.Src);
            context.DrawRectangle(
                new SolidColorBrush(new Vector4(0f, 0f, 0f, 0f)),
                null,
                new Rect(0f, 0f, 32f, 32f));
            context.PopBlendMode();

            context.PushBlendMode(GpuBlendMode.DstOver);
            context.DrawRectangle(
                new SolidColorBrush(new Vector4(1f, 0f, 0f, 0.5f)),
                null,
                new Rect(0f, 0f, 32f, 32f));
            context.PopBlendMode();
        }
    }
}
