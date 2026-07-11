using System.Numerics;
using ProGPU.Vector;
using Xunit;

namespace ProGPU.Tests;

public sealed class PathGeometrySvgParserTests
{
    [Fact]
    public void ParsesSmoothCubicAndQuadraticSegments()
    {
        var geometry = PathGeometry.Parse(
            "M 0 0 C 10 0 20 10 30 10 S 50 20 60 10 Q 70 0 80 10 T 100 10");

        var segments = geometry.Figures[0].Segments;
        var smoothCubic = Assert.IsType<CubicBezierSegment>(segments[1]);
        var smoothQuadratic = Assert.IsType<QuadraticBezierSegment>(segments[3]);
        Assert.Equal(new Vector2(40, 10), smoothCubic.ControlPoint1);
        Assert.Equal(new Vector2(90, 20), smoothQuadratic.ControlPoint);
    }

    [Fact]
    public void ParsesRelativeSmoothCubicSegments()
    {
        var geometry = PathGeometry.Parse("M 1 2 c 3 4 5 6 7 8 s 9 10 11 12");

        var smooth = Assert.IsType<CubicBezierSegment>(geometry.Figures[0].Segments[1]);
        Assert.Equal(new Vector2(10, 12), smooth.ControlPoint1);
        Assert.Equal(new Vector2(17, 20), smooth.ControlPoint2);
        Assert.Equal(new Vector2(19, 22), smooth.Point);
    }

    [Fact]
    public void ParsesAdjacentCompactDecimals()
    {
        var geometry = PathGeometry.Parse("M0 0 C.999.999 2 3 4 5");

        var cubic = Assert.IsType<CubicBezierSegment>(geometry.Figures[0].Segments[0]);
        Assert.Equal(new Vector2(0.999f, 0.999f), cubic.ControlPoint1);
        Assert.Equal(new Vector2(2, 3), cubic.ControlPoint2);
        Assert.Equal(new Vector2(4, 5), cubic.Point);
    }
}
