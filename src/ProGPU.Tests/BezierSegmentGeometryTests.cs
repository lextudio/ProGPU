using System.Numerics;
using ProGPU.Vector;
using Xunit;

namespace ProGPU.Tests;

public sealed class BezierSegmentGeometryTests
{
    [Fact]
    public void SubQuadraticBezierSegmentUsesDeCasteljauControlPoints()
    {
        var start = new Vector2(0f, 0f);
        var segment = new QuadraticBezierSegment(
            new Vector2(10f, 10f),
            new Vector2(20f, 0f));

        Assert.True(BezierSegmentGeometry.TryCreateSubQuadraticBezierSegment(
            start,
            segment,
            0.25f,
            0.75f,
            out var subStart,
            out var subSegment));

        AssertClose(new Vector2(5f, 3.75f), subStart);
        AssertClose(new Vector2(10f, 6.25f), subSegment.ControlPoint);
        AssertClose(new Vector2(15f, 3.75f), subSegment.Point);
    }

    [Fact]
    public void SubCubicBezierSegmentUsesDeCasteljauControlPoints()
    {
        var start = new Vector2(0f, 0f);
        var segment = new CubicBezierSegment(
            new Vector2(10f, 20f),
            new Vector2(20f, 20f),
            new Vector2(30f, 0f));

        Assert.True(BezierSegmentGeometry.TryCreateSubCubicBezierSegment(
            start,
            segment,
            0.25f,
            0.75f,
            out var subStart,
            out var subSegment));

        AssertClose(BezierSegmentGeometry.EvaluateCubic(
            start,
            segment.ControlPoint1,
            segment.ControlPoint2,
            segment.Point,
            0.25f), subStart);
        AssertClose(BezierSegmentGeometry.EvaluateCubic(
            start,
            segment.ControlPoint1,
            segment.ControlPoint2,
            segment.Point,
            0.75f), subSegment.Point);
        AssertClose(new Vector2(12.5f, 16.25f), subSegment.ControlPoint1);
        AssertClose(new Vector2(17.5f, 16.25f), subSegment.ControlPoint2);
    }

    [Fact]
    public void DashedQuadraticBezierSegmentsPreserveNativeBezierSpansAndAdvanceDashState()
    {
        var start = new Vector2(0f, 0f);
        var segment = new QuadraticBezierSegment(
            new Vector2(10f, 10f),
            new Vector2(20f, 0f));
        var pattern = new[] { 8f, 4f };

        Assert.True(BezierSegmentGeometry.TryCreateDashedQuadraticBezierSegments(
            start,
            segment,
            pattern,
            patternIndex: 0,
            distanceInPattern: 0f,
            out var segments,
            out var finalPatternIndex,
            out var finalDistanceInPattern));

        Assert.NotEmpty(segments);
        AssertClose(start, segments[0].Start);
        Assert.All(segments, dash =>
        {
            Assert.True(float.IsFinite(dash.Segment.ControlPoint.X));
            Assert.True(float.IsFinite(dash.Segment.ControlPoint.Y));
            Assert.True(float.IsFinite(dash.Segment.Point.X));
            Assert.True(float.IsFinite(dash.Segment.Point.Y));
        });
        Assert.InRange(finalPatternIndex, 0, pattern.Length - 1);
        Assert.InRange(finalDistanceInPattern, 0f, pattern[finalPatternIndex]);
    }

    [Fact]
    public void DashedCubicBezierSegmentsPreserveNativeBezierSpansAndAdvanceDashState()
    {
        var start = new Vector2(0f, 0f);
        var segment = new CubicBezierSegment(
            new Vector2(10f, 20f),
            new Vector2(20f, 20f),
            new Vector2(30f, 0f));
        var pattern = new[] { 12f, 6f };

        Assert.True(BezierSegmentGeometry.TryCreateDashedCubicBezierSegments(
            start,
            segment,
            pattern,
            patternIndex: 0,
            distanceInPattern: 0f,
            out var segments,
            out var finalPatternIndex,
            out var finalDistanceInPattern));

        Assert.NotEmpty(segments);
        AssertClose(start, segments[0].Start);
        Assert.All(segments, dash =>
        {
            Assert.True(float.IsFinite(dash.Segment.ControlPoint1.X));
            Assert.True(float.IsFinite(dash.Segment.ControlPoint1.Y));
            Assert.True(float.IsFinite(dash.Segment.ControlPoint2.X));
            Assert.True(float.IsFinite(dash.Segment.ControlPoint2.Y));
            Assert.True(float.IsFinite(dash.Segment.Point.X));
            Assert.True(float.IsFinite(dash.Segment.Point.Y));
        });
        Assert.InRange(finalPatternIndex, 0, pattern.Length - 1);
        Assert.InRange(finalDistanceInPattern, 0f, pattern[finalPatternIndex]);
    }

    [Fact]
    public void DashedBezierSegmentsRejectDegenerateCurvesWithoutAdvancingDashState()
    {
        var pattern = new[] { 5f, 5f };

        Assert.False(BezierSegmentGeometry.TryCreateDashedQuadraticBezierSegments(
            Vector2.Zero,
            new QuadraticBezierSegment(Vector2.Zero, Vector2.Zero),
            pattern,
            patternIndex: 1,
            distanceInPattern: 2f,
            out var segments,
            out var finalPatternIndex,
            out var finalDistanceInPattern));

        Assert.Empty(segments);
        Assert.Equal(1, finalPatternIndex);
        Assert.Equal(2f, finalDistanceInPattern);
    }

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.001f, expected.X + 0.001f);
        Assert.InRange(actual.Y, expected.Y - 0.001f, expected.Y + 0.001f);
    }
}
