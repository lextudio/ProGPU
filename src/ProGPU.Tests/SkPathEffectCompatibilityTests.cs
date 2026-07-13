using System.Reflection;
using ProGPU.Vector;
using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkPathEffectCompatibilityTests
{
    [Fact]
    public void FactoriesExposeNativeSurfaceAndParameterNames()
    {
        Assert.Equal(typeof(SKObject), typeof(SKPathEffect).BaseType);
        Assert.Equal(
            10,
            typeof(SKPathEffect).GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length);
        AssertParameters(nameof(SKPathEffect.Create1DPath), 4, "path", "advance", "phase", "style");
        AssertParameters(nameof(SKPathEffect.Create2DLine), 2, "width", "matrix");
        AssertParameters(nameof(SKPathEffect.Create2DPath), 2, "matrix", "path");
        AssertParameters(nameof(SKPathEffect.CreateCompose), 2, "outer", "inner");
        AssertParameters(nameof(SKPathEffect.CreateCorner), 1, "radius");
        AssertParameters(nameof(SKPathEffect.CreateDash), 2, "intervals", "phase");
        AssertParameters(nameof(SKPathEffect.CreateDiscrete), 3, "segLength", "deviation", "seedAssist");
        AssertParameters(nameof(SKPathEffect.CreateSum), 2, "first", "second");
        AssertParameters(nameof(SKPathEffect.CreateTrim), 2, "start", "stop");
        AssertParameters(nameof(SKPathEffect.CreateTrim), 3, "start", "stop", "mode");
        Assert.Equal([0, 1, 2], Enum.GetValues<SKPath1DPathEffectStyle>().Select(static value => (int)value));
        Assert.Equal([0, 1], Enum.GetValues<SKTrimPathEffectMode>().Select(static value => (int)value));
    }

    [Fact]
    public void FactoriesSnapshotMutableInputsAndComposeIndependentGraphs()
    {
        var intervals = new[] { 4f, 2f };
        using var dash = SKPathEffect.CreateDash(intervals, 1f);
        intervals[0] = 99f;
        Assert.Equal(new[] { 4f, 2f }, dash.Intervals);

        using var stamp = new SKPath();
        stamp.AddRect(new SKRect(0f, 0f, 2f, 1f));
        using var path1D = SKPathEffect.Create1DPath(stamp, 3f, 0f, SKPath1DPathEffectStyle.Rotate);
        using var path2D = SKPathEffect.Create2DPath(SKMatrix.Identity, stamp);
        stamp.Reset();
        Assert.False(Assert.IsType<SKPathEffect.Path1DData>(path1D.Data).Path.IsEmpty);
        Assert.False(Assert.IsType<SKPathEffect.Path2DData>(path2D.Data).Path.IsEmpty);

        using var composed = SKPathEffect.CreateCompose(path1D, dash);
        using var sum = SKPathEffect.CreateSum(path2D, dash);
        path1D.Dispose();
        path2D.Dispose();
        dash.Dispose();
        Assert.Equal(SKPathEffect.EffectKind.Compose, composed.Kind);
        Assert.Equal(SKPathEffect.EffectKind.Sum, sum.Kind);
    }

    [Fact]
    public void PaintRetainsDisposedDashEffectWithoutChangingPenOutput()
    {
        var dash = SKPathEffect.CreateDash([6f, 2f], 3f);
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            PathEffect = dash,
        };

        dash.Dispose();
        var pen = Assert.IsType<Pen>(paint.ToPen());

        Assert.Equal(new[] { 3.0, 1.0 }, pen.DashArray);
        Assert.Equal(1.5, pen.DashOffset);
    }

    private static void AssertParameters(string name, int count, params string[] expected)
    {
        var method = typeof(SKPathEffect).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == count);
        Assert.Equal(expected, method.GetParameters().Select(static parameter => parameter.Name));
    }
}
