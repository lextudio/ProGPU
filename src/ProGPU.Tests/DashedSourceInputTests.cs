using System.Numerics;
using ProGPU.Scene;
using ProGPU.Vector;
using Xunit;

namespace ProGPU.Tests;

public sealed class DashedSourceInputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlatDashBodiesKeepGapsAndOriginalTransform(bool affine)
    {
        // Paired with native scene 9850: width 2, [2,1], length 20.
        var path = new PathGeometry();
        var figure = new PathFigure(Vector2.Zero);
        figure.Segments.Add(new LineSegment(new Vector2(20, 0)));
        path.Figures.Add(figure);
        var transform = Matrix4x4.Identity;
        if (affine)
        {
            transform.M11 = 2; transform.M21 = 0.5f; transform.M22 = 3;
            transform.M41 = 5; transform.M42 = 7;
        }
        using var capture = new GpuRenderCommandHitTestCacheBuilder();
        capture.AddCommand(new RenderCommand
        {
            Type = RenderCommandType.DrawPath, Path = path, HitTestId = 91,
            Pen = new Pen(new SolidColorBrush(Vector4.One), 2,
                startLineCap: PenLineCap.Flat, endLineCap: PenLineCap.Flat,
                dashCap: PenLineCap.Flat, dashArray: [2, 1])
        }, transform);
        var hits = capture.BuildIndex().Primitives;
        Assert.Equal(4, hits.Count);
        for (int i = 0; i < hits.Count; i++)
        {
            Assert.Equal(91, hits[i].Id);
            Assert.Equal(GpuHitTestPrimitiveKind.LineStroke, hits[i].Kind);
            Assert.Equal(new Vector4(6 * i, 0, Math.Min(20, 6 * i + 4), 0), hits[i].Data0);
            Assert.Equal(new Vector4(2, 0, 0, 0), hits[i].Data1);
        }
    }
}
