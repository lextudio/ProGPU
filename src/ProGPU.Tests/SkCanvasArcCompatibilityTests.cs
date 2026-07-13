using System.Numerics;
using ProGPU.Scene;
using ProGPU.Vector;
using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkCanvasArcCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DrawArcRecordsNativeOpenOrCenteredTopology(bool useCenter)
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 16f, 16f);
        using var paint = new SKPaint
        {
            Color = SKColors.Red,
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
        };

        canvas.DrawArc(new SKRect(2f, 2f, 14f, 14f), 0f, 90f, useCenter, paint);

        var command = Assert.Single(context.Commands);
        Assert.Equal(RenderCommandType.DrawPath, command.Type);
        Assert.NotNull(command.Brush);
        Assert.Null(command.Pen);
        Assert.True(command.IsEdgeAliased);
        var figure = Assert.Single(command.Path!.Figures);
        Assert.Equal(useCenter ? new Vector2(8f, 8f) : new Vector2(14f, 8f), figure.StartPoint);
        Assert.Equal(useCenter, figure.IsClosed);
        if (useCenter)
        {
            Assert.Collection(
                figure.Segments,
                segment => AssertPointNear(new Vector2(14f, 8f), Assert.IsType<LineSegment>(segment).Point),
                segment => AssertPointNear(new Vector2(8f, 14f), Assert.IsType<ArcSegment>(segment).Point));
        }
        else
        {
            AssertPointNear(
                new Vector2(8f, 14f),
                Assert.IsType<ArcSegment>(Assert.Single(figure.Segments)).Point);
        }
    }

    [Fact]
    public void DrawArcBoundsSweepToOneNativeRevolution()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 16f, 16f);
        using var paint = new SKPaint();

        canvas.DrawArc(new SKRect(2f, 2f, 14f, 14f), 0f, 720f, useCenter: false, paint);

        var figure = Assert.Single(Assert.Single(context.Commands).Path!.Figures);
        Assert.Equal(2, figure.Segments.Count);
        Assert.All(figure.Segments, segment => Assert.IsType<ArcSegment>(segment));
        AssertPointNear(new Vector2(14f, 8f), Assert.IsType<ArcSegment>(figure.Segments[^1]).Point);
    }

    [Fact]
    public void DrawArcPreservesNegativeSweepDirection()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 16f, 16f);
        using var paint = new SKPaint();

        canvas.DrawArc(new SKRect(2f, 2f, 14f, 14f), 0f, -90f, useCenter: false, paint);

        var figure = Assert.Single(Assert.Single(context.Commands).Path!.Figures);
        var arc = Assert.IsType<ArcSegment>(Assert.Single(figure.Segments));
        AssertPointNear(new Vector2(8f, 2f), arc.Point);
        Assert.Equal(SweepDirection.Counterclockwise, arc.SweepDirection);
    }

    [Fact]
    public void DrawArcRejectsInvalidOrDegenerateGeometry()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 16f, 16f);
        using var paint = new SKPaint();

        canvas.DrawArc(SKRect.Empty, 0f, 90f, useCenter: false, paint);
        canvas.DrawArc(new SKRect(2f, 2f, 14f, 14f), float.NaN, 90f, useCenter: false, paint);
        canvas.DrawArc(new SKRect(2f, 2f, 14f, 14f), 0f, float.NaN, useCenter: false, paint);
        canvas.DrawArc(new SKRect(2f, 2f, 14f, 14f), 0f, 0f, useCenter: false, paint);

        Assert.Empty(context.Commands);
        Assert.Throws<ArgumentNullException>(() =>
            canvas.DrawArc(new SKRect(2f, 2f, 14f, 14f), 0f, 90f, useCenter: false, null!));
    }

    [Fact]
    public void DrawArcGpuFillAndCenteredStrokeMatchNativeInteriorSemantics()
    {
        var openFill = RenderArc(SKPaintStyle.Fill, useCenter: false, sweepAngle: 90f);
        Assert.Equal(0, ReadAlpha(openFill, 16, 8, 8));
        Assert.InRange(ReadAlpha(openFill, 16, 11, 11), 248, 255);

        var centeredFill = RenderArc(SKPaintStyle.Fill, useCenter: true, sweepAngle: 90f);
        Assert.InRange(ReadAlpha(centeredFill, 16, 9, 9), 248, 255);
        Assert.Equal(0, ReadAlpha(centeredFill, 16, 7, 9));

        var openStroke = RenderArc(SKPaintStyle.Stroke, useCenter: false, sweepAngle: 360f);
        Assert.Equal(0, ReadAlpha(openStroke, 16, 10, 8));

        var centeredStroke = RenderArc(SKPaintStyle.Stroke, useCenter: true, sweepAngle: 360f);
        Assert.InRange(ReadAlpha(centeredStroke, 16, 10, 8), 248, 255);
    }

    private static byte[] RenderArc(SKPaintStyle style, bool useCenter, float sweepAngle)
    {
        using var surface = SKSurface.Create(
            new SKImageInfo(16, 16, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var paint = new SKPaint
        {
            Color = SKColors.Red,
            Style = style,
            StrokeWidth = 2f,
            IsAntialias = false,
        };
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawArc(
            new SKRect(2f, 2f, 14f, 14f),
            0f,
            sweepAngle,
            useCenter,
            paint);
        surface.Flush();
        using var image = surface.Snapshot();
        return image.Texture.ReadPixels();
    }

    private static byte ReadAlpha(byte[] pixels, int width, int x, int y) =>
        pixels[(y * width + x) * 4 + 3];

    private static void AssertPointNear(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
    }
}
