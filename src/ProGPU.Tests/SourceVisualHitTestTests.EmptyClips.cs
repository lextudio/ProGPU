using System.Numerics;
using ProGPU.Scene;
using ProGPU.Tests.Headless;
using ProGPU.Vector;
using Xunit;

namespace ProGPU.Tests;

public sealed partial class SourceVisualHitTestTests
{
    [Theory]
    [InlineData(false, 0f)]
    [InlineData(false, 1f)]
    [InlineData(true, 0f)]
    [InlineData(true, 1f)]
    public void EmptySourceClipCoverageKeepsFollowingOwnersAndColdWarmOpacityControls(bool cached, float opacity)
    {
        using var atlas = cached ? new PathAtlas(HeadlessWindow.Shared.Context, atlasSize: 64) : null;
        using var capture = atlas is null ? new GpuRenderCommandHitTestCacheBuilder() : new GpuRenderCommandHitTestCacheBuilder(atlas);
        var point = new PathGeometry();
        point.Figures.Add(new PathFigure(new Vector2(5, 5), true));
        PathGeometry[] emptyClips =
        [
            PrimitivePathGeometry.CreateRectangle(5, 5, 0, 30),
            PrimitivePathGeometry.CreateRectangle(5, 5, 30, 0),
            point,
            PrimitivePathGeometry.CreateRoundedRectangle(30, 20, 10, 10, 3, 3),
            PrimitivePathGeometry.CreateRectangle(5, 5, 0, 30).CreateTransformed(Matrix4x4.CreateRotationZ(0.5f)),
            PathGeometry.CombineDeferred(PrimitivePathGeometry.CreateRectangle(0, 0, 10, 10),
                PrimitivePathGeometry.CreateRectangle(30, 20, 10, 10), PathBooleanOperation.Intersect)
        ];
        foreach (var empty in emptyClips)
        {
            var source = new SourceVisual { Opacity = opacity, HitTestId = 42 };
            var commands = source.SourceHitTestCommands;
            commands.Commands.Add(new RenderCommand { Type = RenderCommandType.PushGeometryClip,
                Path = PrimitivePathGeometry.CreateRoundedRectangle(0, 0, 10, 10, 3, 3) });
            commands.Commands.Add(new RenderCommand { Type = RenderCommandType.PushGeometryClip, Path = empty });
            commands.DrawRectangle(new SolidColorBrush(Vector4.One), null, new Rect(0, 0, 60, 60));
            commands.Commands.Add(new RenderCommand { Type = RenderCommandType.PopGeometryClip });
            commands.Commands.Add(new RenderCommand { Type = RenderCommandType.PopGeometryClip });
            commands.DrawRectangle(new SolidColorBrush(Vector4.One), null, new Rect(70, 70, 10, 10));
            for (int frame = 0; frame < 2; frame++)
            {
                capture.Clear();
                capture.AddSourceVisual(source, Matrix4x4.Identity);
                var following = Assert.Single(capture.BuildIndex().Primitives);
                Assert.Equal(42, following.Id);
                Assert.Equal(new Vector2(70, 70), following.BoundsMin);
                Assert.Equal(new Vector2(80, 80), following.BoundsMax);
                Assert.Equal(0U, following.ClipSegmentCount);
            }
            Assert.Equal(0, source.RenderCalls);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyCompilationDoesNotAdmitMissingUnsupportedOrNonfiniteSourceGeometry(bool cached)
    {
        using var atlas = cached ? new PathAtlas(HeadlessWindow.Shared.Context, atlasSize: 64) : null;
        var unknown = new PathGeometry();
        var figure = new PathFigure(Vector2.Zero, false);
        figure.Segments.Add(new UnsupportedClipSegment());
        unknown.Figures.Add(figure);
        var invalidPoint = new PathGeometry();
        invalidPoint.Figures.Add(new PathFigure(new Vector2(float.NaN, 0), true));
        PathGeometry[] invalid =
        [
            new() { IsCombined = true }, unknown, invalidPoint,
            PathGeometry.CombineDeferred(unknown, new PathGeometry(), PathBooleanOperation.Intersect)
        ];
        foreach (var path in invalid)
        {
            using var capture = atlas is null ? new GpuRenderCommandHitTestCacheBuilder() : new GpuRenderCommandHitTestCacheBuilder(atlas);
            var source = new SourceVisual { Opacity = 0, HitTestId = 7 };
            source.SourceHitTestCommands.Commands.Add(new RenderCommand { Type = RenderCommandType.PushGeometryClip, Path = path });
            source.SourceHitTestCommands.DrawRectangle(new SolidColorBrush(Vector4.One), null, new Rect(0, 0, 10, 10));
            source.SourceHitTestCommands.Commands.Add(new RenderCommand { Type = RenderCommandType.PopGeometryClip });
            for (int frame = 0; frame < 2; frame++)
            {
                capture.Clear();
                Assert.Throws<NotSupportedException>(() => capture.AddSourceVisual(source, Matrix4x4.Identity));
                Assert.Throws<InvalidOperationException>(() => capture.BuildIndex());
            }
        }
    }

    [Fact]
    public void PathAtlasRetainsExplicitEmptyProofWithoutChangingTheLegacyResult()
    {
        using var atlas = new PathAtlas(HeadlessWindow.Shared.Context, atlasSize: 64);
        var point = new PathGeometry();
        point.Figures.Add(new PathFigure(new Vector2(5, 5), true));
        for (int frame = 0; frame < 2; frame++)
        {
            Assert.False(atlas.TryGetCompiledHitTestPath(point, out _, out _, out _, out _, out _, out _, out bool empty));
            Assert.True(empty);
            Assert.False(atlas.TryGetCompiledHitTestPath(point, out _, out _, out _, out _, out _, out _));
            var unavailable = new PathGeometry { IsCombined = true };
            Assert.False(atlas.TryGetCompiledHitTestPath(unavailable, out _, out _, out _, out _, out _, out _, out empty));
            Assert.False(empty);
        }
    }

    private sealed class UnsupportedClipSegment : PathSegment { }
}
