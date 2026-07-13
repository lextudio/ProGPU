using System.Reflection;
using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkPaintFillPathApiCompatibilityTests
{
    [Fact]
    public void FillPathFamilyExposesAllNativeOverloads()
    {
        var methods = typeof(SKPaint)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == nameof(SKPaint.GetFillPath))
            .ToArray();

        Assert.Equal(18, methods.Length);
        Assert.All(methods, method => Assert.Equal("src", method.GetParameters()[0].Name));
        Assert.Equal(6, methods.Count(method => method.ReturnType == typeof(SKPath)));
        Assert.Equal(6, methods.Count(method =>
            method.ReturnType == typeof(bool) &&
            method.GetParameters()[1].ParameterType == typeof(SKPath)));
        Assert.Equal(6, methods.Count(method =>
            method.ReturnType == typeof(bool) &&
            method.GetParameters()[1].ParameterType == typeof(SKPathBuilder)));
    }

    [Fact]
    public void FillPathHintsPreserveSourceCoordinateStrokeOutput()
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4f,
            StrokeCap = SKStrokeCap.Round,
        };
        using var source = new SKPath();
        source.MoveTo(0f, 0f);
        source.LineTo(20f, 0f);
        using var baseline = paint.GetFillPath(source);
        using var scaled = paint.GetFillPath(source, 2f);
        using var transformed = paint.GetFillPath(source, SKMatrix.CreateScale(2f, 2f));
        using var culled = paint.GetFillPath(
            source,
            new SKRect(-100f, -100f, 100f, 100f),
            SKMatrix.CreateScale(2f, 2f));

        Assert.NotNull(baseline);
        Assert.Equal(baseline!.ToSvgPathData(), scaled!.ToSvgPathData());
        Assert.Equal(baseline.ToSvgPathData(), transformed!.ToSvgPathData());
        Assert.Equal(baseline.ToSvgPathData(), culled!.ToSvgPathData());
    }

    [Fact]
    public void BuilderAppendsAndObsoletePathOverloadSupportsAliasing()
    {
        using var paint = new SKPaint { Style = SKPaintStyle.Fill };
        using var source = new SKPath();
        source.AddRect(new SKRect(0f, 0f, 10f, 10f));
        using var builder = new SKPathBuilder();
        builder.AddCircle(-10f, -10f, 2f);

        Assert.True(paint.GetFillPath(source, builder));

        using var appended = builder.Detach();
        Assert.Equal(2, appended.Geometry.Figures.Count);
        using var aliased = new SKPath(source);
#pragma warning disable CS0618
        Assert.True(paint.GetFillPath(aliased, aliased));
#pragma warning restore CS0618
        Assert.Equal(source.ToSvgPathData(), aliased.ToSvgPathData());
    }
}
