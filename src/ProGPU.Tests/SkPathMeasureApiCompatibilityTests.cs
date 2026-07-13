using System.Reflection;
using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkPathMeasureApiCompatibilityTests
{
    [Fact]
    public void ConvenienceMethodsReturnNativeFallbacksAndSampledValues()
    {
        using var empty = new SKPathMeasure();
        Assert.Equal(SKPoint.Empty, empty.GetPosition(10f));
        Assert.Equal(SKPoint.Empty, empty.GetTangent(10f));
        Assert.Equal(
            SKMatrix.Empty,
            empty.GetMatrix(10f, SKPathMeasureMatrixFlags.GetPositionAndTangent));
        Assert.Null(empty.GetSegment(0f, 10f, startWithMoveTo: true));

        using var path = new SKPath();
        path.MoveTo(0f, 0f);
        path.LineTo(100f, 0f);
        empty.SetPath(path);

        Assert.InRange(empty.Length, 99.999f, 100.001f);
        Assert.Equal(new SKPoint(25f, 0f), empty.GetPosition(25f));
        Assert.Equal(new SKPoint(1f, 0f), empty.GetTangent(25f));
        var matrix = empty.GetMatrix(
            25f,
            SKPathMeasureMatrixFlags.GetPositionAndTangent);
        Assert.Equal(SKMatrix.CreateTranslation(25f, 0f), matrix);

        using var segment = empty.GetSegment(20f, 40f, startWithMoveTo: true);
        Assert.NotNull(segment);
        Assert.Equal("M20 0L40 0", segment!.ToSvgPathData());
    }

    [Fact]
    public void BuilderSegmentAppendsWithoutIntermediatePathOrContourBreak()
    {
        using var path = new SKPath();
        path.MoveTo(0f, 0f);
        path.LineTo(100f, 0f);
        using var measure = new SKPathMeasure(path);
        using var builder = new SKPathBuilder();
        builder.MoveTo(-10f, 0f);
        builder.LineTo(-5f, 0f);

        Assert.True(measure.GetSegment(20f, 40f, builder, startWithMoveTo: false));

        using var segment = builder.Detach();
        Assert.Equal("M-10 0L-5 0L20 0L40 0", segment.ToSvgPathData());
    }

    [Fact]
    public void ObsoletePathSegmentOverloadReplacesDestination()
    {
        using var path = new SKPath();
        path.MoveTo(0f, 0f);
        path.LineTo(100f, 0f);
        using var measure = new SKPathMeasure(path);
        using var destination = new SKPath();
        destination.MoveTo(-10f, -10f);
        destination.LineTo(-5f, -5f);

#pragma warning disable CS0618
        Assert.True(measure.GetSegment(20f, 40f, destination, startWithMoveTo: true));
#pragma warning restore CS0618

        Assert.Equal("M20 0L40 0", destination.ToSvgPathData());
    }

    [Fact]
    public void PathFamiliesExposeNativeParameterNames()
    {
        AssertParameterNames(
            typeof(SKPath).GetConstructor([typeof(SKPath)]),
            "path");
        AssertParameterNames(
            typeof(SKPath).GetMethod(
                nameof(SKPath.ParseSvgPathData),
                BindingFlags.Public | BindingFlags.Static,
                [typeof(string)]),
            "svgPath");
        AssertParameterNames(
            typeof(SKPath).GetMethod(
                nameof(SKPath.AddPath),
                [typeof(SKPath), typeof(float), typeof(float), typeof(SKPathAddMode)]),
            "other",
            "dx",
            "dy",
            "mode");
        AssertParameterNames(
            typeof(SKPath).GetMethod(
                nameof(SKPath.ArcTo),
                [
                    typeof(SKPoint),
                    typeof(float),
                    typeof(SKPathArcSize),
                    typeof(SKPathDirection),
                    typeof(SKPoint),
                ]),
            "r",
            "xAxisRotate",
            "largeArc",
            "sweep",
            "xy");
        AssertParameterNames(
            typeof(SKPathMeasure).GetMethod(
                nameof(SKPathMeasure.GetSegment),
                [typeof(float), typeof(float), typeof(SKPathBuilder), typeof(bool)]),
            "start",
            "stop",
            "dst",
            "startWithMoveTo");
    }

    private static void AssertParameterNames(MethodBase? method, params string[] expected)
    {
        Assert.NotNull(method);
        Assert.Equal(expected, method!.GetParameters().Select(parameter => parameter.Name));
    }
}
