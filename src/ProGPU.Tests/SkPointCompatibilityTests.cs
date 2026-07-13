using System.Numerics;
using ProGPU.Backend;
using ProGPU.Scene;
using ProGPU.Vector;
using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkPointCompatibilityTests
{
    [Fact]
    public void PointModeValuesMatchNativeSkia()
    {
        Assert.Equal(0, (int)SKPointMode.Points);
        Assert.Equal(1, (int)SKPointMode.Lines);
        Assert.Equal(2, (int)SKPointMode.Polygon);
    }

    [Theory]
    [InlineData(SKStrokeCap.Butt, false)]
    [InlineData(SKStrokeCap.Round, true)]
    [InlineData(SKStrokeCap.Square, false)]
    public void PointsQueueOneAnalyticBatchWithNativeCapSemantics(
        SKStrokeCap cap,
        bool expectedRound)
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 32f, 32f);
        using var paint = new SKPaint
        {
            Color = SKColors.Red,
            IsAntialias = false,
            StrokeCap = cap,
            StrokeWidth = 4f,
            Style = SKPaintStyle.Fill,
        };
        canvas.Translate(3f, 4f);

        canvas.DrawPoints(
            SKPointMode.Points,
            [new SKPoint(5f, 6f), new SKPoint(7f, 8f)],
            paint);

        var command = Assert.Single(context.Commands);
        Assert.Equal(RenderCommandType.DrawPointBatch, command.Type);
        Assert.Equal(2f, command.RadiusX);
        Assert.Equal(expectedRound ? 1 : 0, command.IntParam);
        Assert.True(command.IsEdgeAliased);
        Assert.Equal(3f, command.Transform.M41);
        Assert.Equal(4f, command.Transform.M42);
        Assert.Equal([new Vector2(5f, 6f), new Vector2(7f, 8f)], command.PolylinePoints!);
    }

    [Fact]
    public void ZeroWidthPointUsesDeviceSpaceHairlineSentinel()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 32f, 32f);
        using var paint = new SKPaint { StrokeWidth = 0f };

        canvas.DrawPoint(2f, 3f, paint);

        var command = Assert.Single(context.Commands);
        Assert.Equal(0f, command.RadiusX);
        Assert.Equal([new Vector2(2f, 3f)], command.PolylinePoints!);
    }

    [Fact]
    public void HairlineRetainsCanvasScaleForDeviceSpaceShaderExpansion()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 32f, 32f);
        using var paint = new SKPaint { StrokeWidth = 0f };
        canvas.Scale(4f, 4f);

        canvas.DrawPoint(2f, 3f, paint);

        var command = Assert.Single(context.Commands);
        Assert.Equal(0f, command.RadiusX);
        Assert.Equal(4f, command.Transform.M11);
        Assert.Equal(4f, command.Transform.M22);
    }

    [Fact]
    public void PositiveSubpixelWidthRetainsLocalRadius()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 32f, 32f);
        using var paint = new SKPaint { StrokeWidth = 0.5f };
        canvas.Scale(4f, 4f);

        canvas.DrawPoint(2f, 3f, paint);

        var command = Assert.Single(context.Commands);
        Assert.Equal(0.25f, command.RadiusX);
        Assert.Equal(4f, command.Transform.M11);
        Assert.Equal(4f, command.Transform.M22);
    }

    [Fact]
    public void LineModeDropsOddTailAndForcesStrokeStyle()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 32f, 32f);
        using var paint = new SKPaint
        {
            Color = SKColors.Blue,
            StrokeWidth = 3f,
            Style = SKPaintStyle.Fill,
        };

        canvas.DrawPoints(
            SKPointMode.Lines,
            [new SKPoint(1f, 2f), new SKPoint(3f, 4f), new SKPoint(9f, 10f)],
            paint);

        var command = Assert.Single(context.Commands);
        Assert.Equal(RenderCommandType.DrawPath, command.Type);
        Assert.Null(command.Brush);
        Assert.Equal(3f, command.Pen!.Thickness);
        var figure = Assert.Single(command.Path!.Figures);
        Assert.Equal(new Vector2(1f, 2f), figure.StartPoint);
        Assert.Equal(new Vector2(3f, 4f), Assert.IsType<LineSegment>(Assert.Single(figure.Segments)).Point);
        Assert.False(figure.IsClosed);
    }

    [Fact]
    public void PolygonModeCreatesOneOpenPolyline()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 32f, 32f);
        using var paint = new SKPaint { StrokeWidth = 2f };

        canvas.DrawPoints(
            SKPointMode.Polygon,
            [new SKPoint(1f, 2f), new SKPoint(3f, 4f), new SKPoint(5f, 6f)],
            paint);

        var command = Assert.Single(context.Commands);
        var figure = Assert.Single(command.Path!.Figures);
        Assert.Equal(new Vector2(1f, 2f), figure.StartPoint);
        Assert.Collection(
            figure.Segments,
            segment => Assert.Equal(new Vector2(3f, 4f), Assert.IsType<LineSegment>(segment).Point),
            segment => Assert.Equal(new Vector2(5f, 6f), Assert.IsType<LineSegment>(segment).Point));
        Assert.False(figure.IsClosed);
    }

    [Theory]
    [InlineData(SKPointMode.Lines)]
    [InlineData(SKPointMode.Polygon)]
    public void ConnectedModesIgnoreSinglePoint(SKPointMode mode)
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 32f, 32f);
        using var paint = new SKPaint();

        canvas.DrawPoints(mode, [new SKPoint(1f, 2f)], paint);

        Assert.Empty(context.Commands);
    }

    [Fact]
    public void ColorOverloadUsesSourceBlendMode()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 32f, 32f);

        canvas.DrawPoint(2f, 3f, SKColors.Lime);

        Assert.Collection(
            context.Commands,
            push =>
            {
                Assert.Equal(RenderCommandType.PushBlendMode, push.Type);
                Assert.Equal((int)GpuBlendMode.Src, push.IntParam);
            },
            point => Assert.Equal(RenderCommandType.DrawPointBatch, point.Type),
            pop => Assert.Equal(RenderCommandType.PopBlendMode, pop.Type));
    }

    [Fact]
    public void DrawPointsValidatesRequiredReferences()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 32f, 32f);
        using var paint = new SKPaint();

        Assert.Throws<ArgumentNullException>(() => canvas.DrawPoints(SKPointMode.Points, null!, paint));
        Assert.Throws<ArgumentNullException>(() => canvas.DrawPoints(SKPointMode.Points, [], null!));
        Assert.Throws<ArgumentNullException>(() => canvas.DrawPoint(SKPoint.Empty, (SKPaint)null!));
    }

    [Fact]
    public void AppendingContextComposesTranslationWithoutCopyingPointArray()
    {
        var points = new[] { new Vector2(1f, 2f), new Vector2(3f, 4f) };
        var source = new DrawingContext();
        source.DrawPointBatch(
            new SolidColorBrush(Vector4.One),
            points,
            radius: 2f,
            round: true,
            Matrix4x4.Identity);
        var target = new DrawingContext();

        target.Append(source, new Vector2(5f, 6f));

        var command = Assert.Single(target.Commands);
        Assert.Same(points, command.PolylinePoints);
        Assert.Equal(5f, command.Transform.M41);
        Assert.Equal(6f, command.Transform.M42);
    }
}
