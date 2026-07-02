using System.Numerics;
using ProGPU.Vector;
using Xunit;

namespace ProGPU.Tests;

public sealed class BoundsHitTestingTests
{
    [Fact]
    public void ContainsPointAcceptsPointInsideBounds()
    {
        Assert.True(BoundsHitTesting.ContainsPoint(
            new Vector2(5f, 6f),
            new Vector2(1f, 2f),
            new Vector2(10f, 12f),
            tolerance: 0f,
            relativeTolerance: false));
    }

    [Fact]
    public void ContainsPointInflatesByAbsoluteTolerance()
    {
        Assert.True(BoundsHitTesting.ContainsPoint(
            new Vector2(11f, 6f),
            new Vector2(1f, 2f),
            new Vector2(10f, 12f),
            tolerance: 1f,
            relativeTolerance: false));
    }

    [Fact]
    public void ContainsPointInflatesByRelativeTolerance()
    {
        Assert.True(BoundsHitTesting.ContainsPoint(
            new Vector2(11f, 6f),
            new Vector2(1f, 2f),
            new Vector2(10f, 12f),
            tolerance: 0.1f,
            relativeTolerance: true));
    }

    [Fact]
    public void ContainsPointRejectsInvalidBounds()
    {
        Assert.False(BoundsHitTesting.ContainsPoint(
            new Vector2(5f, 6f),
            new Vector2(float.NaN, 2f),
            new Vector2(10f, 12f),
            tolerance: 0f,
            relativeTolerance: false));
    }
}
