using System.Numerics;
using ProGPU.Vector;
using Xunit;

namespace ProGPU.Tests;

public sealed class DashPatternTests
{
    [Fact]
    public void TryCreateDuplicatesOddDashArraysAndScalesByThickness()
    {
        Assert.True(DashPattern.TryCreate(
            new[] { 1.0, 2.0, 3.0 },
            dashOffset: 0.0,
            strokeThickness: 2.0,
            out var pattern));

        Assert.Equal(new[] { 2f, 4f, 6f, 2f, 4f, 6f }, pattern.Intervals.ToArray());
        Assert.Equal(0, pattern.InitialIndex);
        Assert.Equal(0f, pattern.InitialDistance);
    }

    [Fact]
    public void TryCreateConvertsZeroLengthDrawnDashesToStrokeThickness()
    {
        Assert.True(DashPattern.TryCreate(
            new[] { 0.0, 2.0 },
            dashOffset: 0.0,
            strokeThickness: 3.0,
            out var pattern));

        Assert.Equal(new[] { 3f, 6f }, pattern.Intervals.ToArray());
    }

    [Fact]
    public void TryCreateRejectsZeroLengthGaps()
    {
        Assert.False(DashPattern.TryCreate(
            new[] { 1.0, 0.0 },
            dashOffset: 0.0,
            strokeThickness: 2.0,
            out _));
    }

    [Fact]
    public void TryCreateLineSegmentsPreservesDashOffsetAndAdvancesState()
    {
        Assert.True(DashPattern.TryCreate(
            new[] { 2.0, 2.0 },
            dashOffset: 1.0,
            strokeThickness: 1.0,
            out var pattern));

        Assert.True(pattern.TryCreateLineSegments(
            Vector2.Zero,
            new Vector2(10f, 0f),
            pattern.InitialIndex,
            pattern.InitialDistance,
            out var segments,
            out var finalPatternIndex,
            out var finalDistanceInPattern));

        Assert.Equal(3, segments.Length);
        AssertClose(new Vector2(0f, 0f), segments[0].Start);
        AssertClose(new Vector2(1f, 0f), segments[0].End);
        AssertClose(new Vector2(3f, 0f), segments[1].Start);
        AssertClose(new Vector2(5f, 0f), segments[1].End);
        AssertClose(new Vector2(7f, 0f), segments[2].Start);
        AssertClose(new Vector2(9f, 0f), segments[2].End);
        Assert.Equal(1, finalPatternIndex);
        Assert.Equal(1f, finalDistanceInPattern);
    }

    [Fact]
    public void TryCreateLineSegmentsNormalizesSuppliedDashState()
    {
        Assert.True(DashPattern.TryCreate(
            new[] { 2.0, 2.0 },
            dashOffset: 0.0,
            strokeThickness: 1.0,
            out var pattern));

        Assert.True(pattern.TryCreateLineSegments(
            Vector2.Zero,
            new Vector2(4f, 0f),
            patternIndex: 0,
            distanceInPattern: 2f,
            out var segments,
            out var finalPatternIndex,
            out var finalDistanceInPattern));

        Assert.Single(segments);
        AssertClose(new Vector2(2f, 0f), segments[0].Start);
        AssertClose(new Vector2(4f, 0f), segments[0].End);
        Assert.Equal(1, finalPatternIndex);
        Assert.Equal(0f, finalDistanceInPattern);
    }

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
        Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
    }
}
