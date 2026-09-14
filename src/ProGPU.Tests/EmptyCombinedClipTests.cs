using System.Numerics;
using ProGPU.Vector;
using Xunit;

namespace ProGPU.Tests;

public sealed class EmptyCombinedClipTests
{
    [Theory]
    [InlineData(0, 2)] // MIL Union / ProGPU Union
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 0)] // MIL Exclude / ProGPU Difference
    public void EmptyOperandsKeepOriginalFillAndNestedPath(int milMode, int operation)
    {
        // Paired with native empty_combined_clips_preserve_original_fill_and_scope.
        for (int emptySide = 0; emptySide < 3; emptySide++)
        foreach (var fill in new[] { FillRule.EvenOdd, FillRule.Nonzero })
        foreach (bool nested in new[] { false, true })
        {
            var source = new PathGeometry { FillRule = fill };
            var figure = new PathFigure(new Vector2(10, 20), isClosed: true);
            figure.Segments.Add(new LineSegment(new Vector2(30, 20)));
            figure.Segments.Add(new LineSegment(new Vector2(30, 40)));
            figure.Segments.Add(new LineSegment(new Vector2(10, 40)));
            source.Figures.Add(figure);
            var a = emptySide == 0 || emptySide == 2 ? new PathGeometry() : source;
            var b = emptySide == 1 || emptySide == 2 ? new PathGeometry() : source;
            var result = PathGeometry.CombineDeferred(a, b, (PathBooleanOperation)operation);
            if (nested) result = PathGeometry.CombineDeferred(new PathGeometry(), result, PathBooleanOperation.Union);
            result = result.CreateTransformed(Matrix4x4.CreateScale(2, 3, 1) * Matrix4x4.CreateTranslation(5, 7, 0));
            var (records, segments) = PathAtlas.CompilePath(result, out float x, out float y, out float right, out float bottom);
            bool survives = emptySide != 2 && (milMode == 0 || milMode == 2 || (milMode == 3 && emptySide == 1));
            if (!survives)
            {
                Assert.Empty(records);
                Assert.Empty(segments);
                continue;
            }
            Assert.Single(records);
            Assert.Equal(4, segments.Length);
            var (originalRecords, _) = PathAtlas.CompilePath(source, out _, out _, out _, out _);
            Assert.Equal(originalRecords[0].FillRule, records[0].FillRule);
            Assert.Equal((25f, 67f, 65f, 127f), (x, y, right, bottom));
        }
    }
}
