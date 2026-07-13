using System.Numerics;
using ProGPU.Scene;
using ProGPU.Vector;
using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkGeometryValueCompatibilityTests
{
    [Fact]
    public void PointMathMatchesSkiaSharpValueSemantics()
    {
        var point = new SKPoint(3f, 4f);

        Assert.Equal(5f, point.Length);
        Assert.Equal(25f, point.LengthSquared);
        Assert.Equal(new SKPoint(0.6f, 0.8f), SKPoint.Normalize(point));
        Assert.True(float.IsNaN(SKPoint.Normalize(SKPoint.Empty).X));
        Assert.Equal(5f, SKPoint.Distance(SKPoint.Empty, point));
        Assert.Equal(25f, SKPoint.DistanceSquared(SKPoint.Empty, point));
        Assert.Equal(new SKPoint(2f, -29f), SKPoint.Reflect(new SKPoint(2f, -3f), new SKPoint(0f, 1f)));
        Assert.Equal(new SKPoint(5f, 7f), point + new SKSize(2f, 3f));
        Assert.Equal(new SKPoint(2f, 2f), point - new SKPointI(1, 2));

        point.Offset(-1f, 2f);
        Assert.Equal(new SKPoint(2f, 6f), point);
        Assert.Equal("{X=2, Y=6}", point.ToString());
    }

    [Fact]
    public void RectQueriesPreserveNativeBoundaryAndDegenerateSemantics()
    {
        var rect = new SKRect(10f, 20f, 30f, 50f);

        Assert.True(SKRect.Empty.IsEmpty);
        Assert.False(new SKRect(0f, 0f, -1f, -1f).IsEmpty);
        Assert.True(rect.Contains(new SKPoint(10f, 20f)));
        Assert.True(rect.Contains(29.999f, 49.999f));
        Assert.False(rect.Contains(30f, 25f));
        Assert.False(rect.Contains(20f, 50f));
        Assert.Equal(new SKRect(10f, 20f, 30f, 50f), new SKRect(30f, 50f, 10f, 20f).Standardized);

        var touching = new SKRect(30f, 20f, 40f, 50f);
        Assert.False(rect.IntersectsWith(touching));
        Assert.True(rect.IntersectsWithInclusive(touching));
        Assert.Equal(new SKRect(30f, 20f, 30f, 50f), SKRect.Intersect(rect, touching));
    }

    [Fact]
    public void RectMutationUnionAndAspectResizeMatchNativeResults()
    {
        var rect = new SKRect(10f, 20f, 30f, 50f);
        Assert.Equal(new SKRect(0f, 0f, 30f, 50f), SKRect.Union(rect, SKRect.Empty));
        Assert.Equal(new SKRect(10f, 30f, 30f, 40f), rect.AspectFit(new SKSize(40f, 20f)));
        Assert.Equal(new SKRect(-10f, 20f, 50f, 50f), rect.AspectFill(new SKSize(40f, 20f)));
        Assert.Equal(new SKRect(20f, 35f, 20f, 35f), rect.AspectFit(SKSize.Empty));

        rect.Location = new SKPoint(3f, 4f);
        Assert.Equal(new SKRect(3f, 4f, 23f, 34f), rect);
        rect.Size = new SKSize(7f, 9f);
        Assert.Equal(new SKRect(3f, 4f, 10f, 13f), rect);
        rect.Inflate(2f, 3f);
        rect.Offset(new SKPoint(1f, -1f));
        Assert.Equal(new SKRect(2f, 0f, 13f, 15f), rect);
    }

    [Fact]
    public void CanvasPointOverloadsRecordTheExistingScalarCommands()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 64f, 64f);
        using var stroke = new SKPaint { Style = SKPaintStyle.Stroke };
        using var fill = new SKPaint { Style = SKPaintStyle.Fill };

        canvas.DrawLine(new SKPoint(2f, 3f), new SKPoint(11f, 13f), stroke);
        canvas.DrawCircle(new SKPoint(17f, 19f), 5f, fill);

        Assert.Equal(2, context.Commands.Count);
        var line = context.Commands[0];
        Assert.Equal(RenderCommandType.DrawPath, line.Type);
        var figure = Assert.Single(line.Path!.Figures);
        Assert.Equal(new Vector2(2f, 3f), figure.StartPoint);
        Assert.Equal(new Vector2(11f, 13f), Assert.IsType<LineSegment>(Assert.Single(figure.Segments)).Point);

        var circle = context.Commands[1];
        Assert.Equal(RenderCommandType.DrawCircle, circle.Type);
        Assert.Equal(new Vector2(17f, 19f), circle.Position2);
        Assert.Equal(5f, circle.RadiusX);
    }

    [Fact]
    public void SizesPreserveNativeValueAndCheckedConversionSemantics()
    {
        var size = new SKSize(new SKPoint(3.75f, 4.25f));

        Assert.Equal(new SKPoint(3.75f, 4.25f), size.ToPoint());
        Assert.Equal(new SKSizeI(3, 4), size.ToSizeI());
        Assert.Equal(new SKSize(5.75f, 7.25f), size + new SKSize(2f, 3f));
        Assert.Equal(new SKSize(1.75f, 1.25f), SKSize.Subtract(size, new SKSize(2f, 3f)));
        Assert.False(new SKSize(-1f, -1f).IsEmpty);
        Assert.Equal("{Width=3.75, Height=4.25}", size.ToString());
        Assert.Throws<OverflowException>(() => new SKSize(float.PositiveInfinity, 1f).ToSizeI());

        var integer = new SKSizeI(new SKPointI(5, 7));
        Assert.Equal(new SKPointI(5, 7), integer.ToPointI());
        Assert.Equal(new SKSizeI(8, 11), SKSizeI.Add(integer, new SKSizeI(3, 4)));
        Assert.Equal(new SKSizeI(2, 3), integer - new SKSizeI(3, 4));
        Assert.Equal(new SKSize(5f, 7f), (SKSize)integer);
        Assert.Equal("{Width=5, Height=7}", integer.ToString());
    }

    [Fact]
    public void IntegerRectQueriesAndMutationMatchNativeSemantics()
    {
        var rect = new SKRectI(10, 20, 30, 50);

        Assert.True(SKRectI.Empty.IsEmpty);
        Assert.False(new SKRectI(0, 0, -1, -1).IsEmpty);
        Assert.True(rect.Contains(new SKPointI(10, 20)));
        Assert.True(rect.Contains(29, 49));
        Assert.False(rect.Contains(30, 25));
        Assert.Equal(new SKRectI(10, 20, 30, 50), new SKRectI(30, 50, 10, 20).Standardized);

        var touching = new SKRectI(30, 20, 40, 50);
        Assert.False(rect.IntersectsWith(touching));
        Assert.True(rect.IntersectsWithInclusive(touching));
        Assert.Equal(new SKRectI(30, 20, 30, 50), SKRectI.Intersect(rect, touching));

        rect.Location = new SKPointI(3, 4);
        rect.Size = new SKSizeI(7, 9);
        rect.Inflate(new SKSizeI(2, 3));
        rect.Offset(1, -1);
        Assert.Equal(new SKRectI(2, 0, 13, 15), rect);
    }

    [Fact]
    public void IntegerRectRoundingPreservesOutwardAndInwardRules()
    {
        var rect = new SKRect(1.2f, 2.8f, 10.7f, 20.1f);

        Assert.Equal(new SKRectI(2, 3, 11, 21), SKRectI.Ceiling(rect));
        Assert.Equal(new SKRectI(1, 2, 11, 21), SKRectI.Ceiling(rect, outwards: true));
        Assert.Equal(new SKRectI(1, 2, 10, 20), SKRectI.Floor(rect));
        Assert.Equal(new SKRectI(2, 3, 10, 20), SKRectI.Floor(rect, inwards: true));
        Assert.Equal(new SKRectI(1, 3, 11, 20), SKRectI.Round(rect));
        Assert.Equal(new SKRectI(1, 2, 10, 20), SKRectI.Truncate(rect));

        var reversed = new SKRect(10.7f, 20.1f, 1.2f, 2.8f);
        Assert.Equal(new SKRectI(11, 21, 1, 2), SKRectI.Ceiling(reversed, outwards: true));
        Assert.Equal(new SKRectI(10, 20, 2, 3), SKRectI.Floor(reversed, inwards: true));
    }

    [Fact]
    public void IntegerRectAspectResizeAndUnionUseFloatCompatibilityPath()
    {
        var rect = new SKRectI(10, 20, 30, 50);

        Assert.Equal(new SKRectI(10, 30, 30, 40), rect.AspectFit(new SKSizeI(40, 20)));
        Assert.Equal(new SKRectI(-10, 20, 50, 50), rect.AspectFill(new SKSizeI(40, 20)));
        Assert.Equal(new SKRectI(20, 35, 20, 35), rect.AspectFit(SKSizeI.Empty));
        Assert.Equal(new SKRectI(0, 0, 30, 50), SKRectI.Union(rect, SKRectI.Empty));
        Assert.Equal(new SKRect(10f, 20f, 30f, 50f), (SKRect)rect);
    }
}
