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
    [Theory]
    [InlineData(VertexColorBlendMode.Src, 255, 0, 0, 255)]
    [InlineData(VertexColorBlendMode.Dst, 0, 0, 255, 255)]
    [InlineData(VertexColorBlendMode.Plus, 255, 0, 255, 255)]
    [InlineData(VertexColorBlendMode.Modulate, 0, 0, 0, 255)]
    public void VertexMeshColorBlendMatchesSkia(
        VertexColorBlendMode mode,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        var window = HeadlessWindow.Shared;
        window.Resize(32, 32);
        window.Content = new VertexMeshVisual(mode);

        try
        {
            window.Render();
            var pixel = ReadPixel(window.ReadPixels(), window.Width, x: 8, y: 8);
            Assert.InRange(pixel.R, Math.Max(0, red - 2), Math.Min(255, red + 2));
            Assert.InRange(pixel.G, Math.Max(0, green - 2), Math.Min(255, green + 2));
            Assert.InRange(pixel.B, Math.Max(0, blue - 2), Math.Min(255, blue + 2));
            Assert.InRange(pixel.A, Math.Max(0, alpha - 2), Math.Min(255, alpha + 2));
        }
        finally
        {
            window.Content = null;
        }
    }

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

    [Fact]
    public void PointBatchRendersSquareAndRoundAnalyticCoverage()
    {
        var window = HeadlessWindow.Shared;
        window.Resize(32, 32);
        window.Content = new PointBatchVisual();

        try
        {
            window.Render();
            var pixels = window.ReadPixels();

            var squareCenter = ReadPixel(pixels, window.Width, x: 8, y: 8);
            Assert.InRange(squareCenter.R, 248, 255);
            Assert.InRange(squareCenter.G, 0, 4);
            Assert.InRange(squareCenter.B, 0, 4);

            var squareCorner = ReadPixel(pixels, window.Width, x: 5, y: 5);
            Assert.InRange(squareCorner.R, 248, 255);

            var roundCenter = ReadPixel(pixels, window.Width, x: 20, y: 8);
            Assert.InRange(roundCenter.R, 0, 4);
            Assert.InRange(roundCenter.G, 0, 4);
            Assert.InRange(roundCenter.B, 248, 255);

            var roundCorner = ReadPixel(pixels, window.Width, x: 17, y: 5);
            Assert.InRange(roundCorner.R, 0, 4);
            Assert.InRange(roundCorner.G, 0, 4);
            Assert.InRange(roundCorner.B, 0, 4);

            var hairlineCenter = ReadPixel(pixels, window.Width, x: 16, y: 20);
            Assert.InRange(hairlineCenter.R, 0, 4);
            Assert.InRange(hairlineCenter.G, 248, 255);
            Assert.InRange(hairlineCenter.B, 0, 4);
            var hairlineNeighbor = ReadPixel(pixels, window.Width, x: 15, y: 20);
            Assert.InRange(hairlineNeighbor.R, 0, 4);
            Assert.InRange(hairlineNeighbor.G, 0, 4);
            Assert.InRange(hairlineNeighbor.B, 0, 4);

            var subpixelLeft = ReadPixel(pixels, window.Width, x: 7, y: 20);
            Assert.InRange(subpixelLeft.R, 248, 255);
            Assert.InRange(subpixelLeft.G, 248, 255);
            Assert.InRange(subpixelLeft.B, 0, 4);
            var subpixelOutside = ReadPixel(pixels, window.Width, x: 6, y: 20);
            Assert.InRange(subpixelOutside.R, 0, 4);
            Assert.InRange(subpixelOutside.G, 0, 4);
            Assert.InRange(subpixelOutside.B, 0, 4);
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

    private sealed class VertexMeshVisual : FrameworkElement
    {
        private readonly VertexColorBlendMode _mode;
        private readonly VertexMesh2D _mesh = new(
            VertexMeshTopology.Triangles,
            new[]
            {
                new Vector2(0f, 0f),
                new Vector2(32f, 0f),
                new Vector2(0f, 32f),
            },
            colors: new[]
            {
                new Vector4(0f, 0f, 1f, 1f),
                new Vector4(0f, 0f, 1f, 1f),
                new Vector4(0f, 0f, 1f, 1f),
            });

        public VertexMeshVisual(VertexColorBlendMode mode)
        {
            _mode = mode;
            Width = 32f;
            Height = 32f;
        }

        public override void OnRender(DrawingContext context)
        {
            context.DrawVertexMesh(
                new SolidColorBrush(new Vector4(1f, 0f, 0f, 1f)),
                _mesh,
                _mode);
        }
    }

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

    private sealed class PointBatchVisual : FrameworkElement
    {
        private static readonly Vector2[] SquarePoint = [new(8f, 8f)];
        private static readonly Vector2[] RoundPoint = [new(20f, 8f)];
        private static readonly Vector2[] HairlinePoint = [new(4f, 5f)];
        private static readonly Vector2[] SubpixelPoint = [new(2f, 5f)];

        public PointBatchVisual()
        {
            Width = 32f;
            Height = 32f;
        }

        public override void OnRender(DrawingContext context)
        {
            context.DrawPointBatch(
                new SolidColorBrush(new Vector4(1f, 0f, 0f, 1f)),
                SquarePoint,
                radius: 3f,
                round: false,
                Matrix4x4.Identity,
                isEdgeAliased: true);
            context.DrawPointBatch(
                new SolidColorBrush(new Vector4(0f, 0f, 1f, 1f)),
                RoundPoint,
                radius: 3f,
                round: true,
                Matrix4x4.Identity,
                isEdgeAliased: true);
            context.DrawPointBatch(
                new SolidColorBrush(new Vector4(0f, 1f, 0f, 1f)),
                HairlinePoint,
                radius: 0f,
                round: false,
                Matrix4x4.CreateScale(4f, 4f, 1f),
                isEdgeAliased: true);
            context.DrawPointBatch(
                new SolidColorBrush(new Vector4(1f, 1f, 0f, 1f)),
                SubpixelPoint,
                radius: 0.25f,
                round: false,
                Matrix4x4.CreateScale(4f, 4f, 1f),
                isEdgeAliased: true);
        }
    }
}
