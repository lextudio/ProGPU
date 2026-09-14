using System.Numerics;
using ProGPU.Scene;
using ProGPU.Backend;
using Xunit;

namespace ProGPU.Tests;

public sealed class SourceRectangleHitTestTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingularSourceRectanglesOmitOnlyTheirOwnInput(bool pointOnly)
    {
        // Paired with native scene 9842: collapsed, rank-one, tiny and mirrored.
        foreach (var linear in new[] { new Vector4(0, 0, 0, 0), new(1, 0, 0, 0),
            new(1, 2, 2, 4), new(1e-12f, 0, 0, 1), new(-1, 0, 0, 1) })
        {
            var transform = Matrix4x4.Identity;
            transform.M11 = linear.X; transform.M12 = linear.Y;
            transform.M21 = linear.Z; transform.M22 = linear.W;
            transform.M41 = 4; transform.M42 = 5;
            using var capture = new GpuRenderCommandHitTestCacheBuilder();
            capture.AddCommand(pointOnly ? new RenderCommand
            {
                Type = RenderCommandType.PushOpacity, FontSize = 1, HitTestId = 17,
                SourceHitGeometry = new(SourceHitTestGeometryKind.PointRectangleBegin, new(0, 0, 20, 20))
            } : new RenderCommand
            {
                Type = RenderCommandType.PushClip, IsImageHitTestScope = true,
                Rect = new Rect(0, 0, 20, 20), HitTestId = 17
            }, transform);
            capture.AddCommand(pointOnly ? new RenderCommand
            {
                Type = RenderCommandType.PopOpacity,
                SourceHitGeometry = new(SourceHitTestGeometryKind.PointRectangleEnd, default)
            } : new RenderCommand { Type = RenderCommandType.PopClip }, transform);
            capture.AddCommand(new RenderCommand
            {
                Type = RenderCommandType.PushClip, IsImageHitTestScope = true,
                Rect = new Rect(0, 0, 20, 20), HitTestId = 18
            }, Matrix4x4.Identity);
            capture.AddCommand(new RenderCommand { Type = RenderCommandType.PopClip }, Matrix4x4.Identity);
            var hits = capture.BuildIndex().Primitives;
            bool singular = (double)linear.X * linear.W == (double)linear.Y * linear.Z;
            Assert.Equal(singular ? 1 : 2, hits.Count);
            Assert.Equal(18, hits[^1].Id);
            Assert.Equal(Vector2.Zero, hits[^1].BoundsMin);
            Assert.Equal(new Vector2(20), hits[^1].BoundsMax);
            if (!singular) Assert.Equal(17, hits[0].Id);
        }
    }
}
