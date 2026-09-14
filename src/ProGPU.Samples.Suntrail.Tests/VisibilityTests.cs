using ProGPU.GameEngine.Rendering;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class VisibilityTests
{
    [Fact]
    public void VisibleRangeCoversEveryIntersectingFootprintAcrossRandomViewports()
    {
        var random = new Random(73109);
        for (int sample = 0; sample < 2000; sample++)
        {
            float origin = random.Next(-10000, 10000), step = random.Next(1, 200);
            int count = random.Next(0, 1000);
            float left = random.Next(-12000, 120000), right = left + random.Next(0, 4000);
            float before = random.Next(0, 300), after = random.Next(0, 300);
            var range = VisibleCellRange.Intersect(origin, step, count, left, right, before, after);
            Assert.InRange(range.Start, 0, count); Assert.InRange(range.End, range.Start, count);
            int actual = 0;
            for (int cell = 0; cell < count; cell++)
            {
                float anchor = origin + cell * step;
                if (anchor - before <= right && anchor + after >= left)
                { Assert.InRange(cell, range.Start, range.End - 1); actual++; }
            }
            Assert.InRange(range.Count, actual, Math.Min(count, actual + 2));
        }
    }

    [Fact]
    public void LevelLengthDoesNotIncreaseSelectedWorkForTheSameViewport()
    {
        var small = VisibleCellRange.Intersect(16, 72, 500, 10000, 11734, 12, 108);
        var large = VisibleCellRange.Intersect(16, 72, 100_000_000, 10000, 11734, 12, 108);
        Assert.Equal(small, large); Assert.InRange(large.Count, 1, 29);
        Assert.Equal(0, VisibleCellRange.Intersect(0, 72, 500, -10000, -9000, 12, 108).Count);
        Assert.Equal(0, VisibleCellRange.Intersect(0, 72, 500, 100000, 101000, 12, 108).Count);
    }

    [Theory]
    [InlineData(0)] [InlineData(72)] [InlineData(144)]
    public void ExactTouchingEdgesRemainCandidates(float left)
    {
        var range = VisibleCellRange.Intersect(0, 72, 10, left, left, 0, 0);
        int cell = (int)(left / 72);
        Assert.InRange(cell, range.Start, range.End - 1);
    }

    [Fact]
    public void InvalidBoundsAndSpacingAreExplicitFailures()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VisibleCellRange.Intersect(0, 0, 10, 0, 100, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => VisibleCellRange.Intersect(0, 1, -1, 0, 100, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => VisibleCellRange.Intersect(0, 1, 10, 100, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => VisibleCellRange.Intersect(0, 1, 10, 0, float.NaN, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => VisibleCellRange.Intersect(0, 1, 10, 0, 100, -1, 0));
    }
}
