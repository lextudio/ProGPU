using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using ProGPU.Scene;
using Xunit;

namespace ProGPU.Tests.Headless;

[Collection("HeadlessTests")]
public sealed class GdiNativeContextTransformTests
{
    [Fact]
    public void NativeContextOuterTransformComposesAfterClientTranslationExactlyOnce()
    {
        var context = new DrawingContext();
        var outerTransform = Matrix4x4.CreateScale(2f, 3f, 1f)
            * Matrix4x4.CreateTranslation(11f, 13f, 0f);

        using (var graphics = Graphics.FromProGpuDrawingContext(context, outerTransform))
        using (var brush = new SolidBrush(Color.CornflowerBlue))
        {
            graphics.TranslateTransform(5f, 7f);
            graphics.FillRectangle(brush, 1f, 2f, 3f, 4f);
        }

        var command = Assert.Single(context.Commands);
        Assert.Equal(RenderCommandType.DrawRect, command.Type);
        Assert.Equal(new Rect(23f, 40f, 6f, 12f), command.Rect);
        Assert.True(command.Transform.IsIdentity || command.Transform == default);
    }

    [Fact]
    public void NativeContextOuterTransformFlowsToPathCommandOnce()
    {
        var context = new DrawingContext();
        var outerTransform = Matrix4x4.CreateScale(2f, 3f, 1f)
            * Matrix4x4.CreateTranslation(11f, 13f, 0f);
        var expected = Matrix4x4.CreateTranslation(5f, 7f, 0f) * outerTransform;

        using (var graphics = Graphics.FromProGpuDrawingContext(context, outerTransform))
        using (var brush = new SolidBrush(Color.CornflowerBlue))
        using (var path = new GraphicsPath())
        {
            graphics.TranslateTransform(5f, 7f);
            path.AddRectangle(new RectangleF(1f, 2f, 3f, 4f));
            graphics.FillPath(brush, path);
        }

        var command = Assert.Single(context.Commands);
        Assert.Equal(RenderCommandType.DrawPath, command.Type);
        Assert.Equal(expected, command.Transform);
    }

    [Fact]
    public void ResetAndTransformSetterCannotEraseNativeContextOuterTransform()
    {
        var context = new DrawingContext();
        var outerTransform = Matrix4x4.CreateScale(2f, 3f, 1f)
            * Matrix4x4.CreateTranslation(11f, 13f, 0f);
        var clientAndOuterTransform = Matrix4x4.CreateTranslation(5f, 7f, 0f)
            * outerTransform;

        using (var graphics = Graphics.FromProGpuDrawingContext(context, clientAndOuterTransform))
        using (var brush = new SolidBrush(Color.CornflowerBlue))
        using (var replacementWorldTransform = new Matrix(1f, 0f, 0f, 1f, 4f, 6f))
        {
            graphics.TranslateTransform(100f, 200f);
            graphics.ResetTransform();
            Assert.True(graphics.Transform.Value.IsIdentity);
            graphics.FillRectangle(brush, 1f, 2f, 3f, 4f);

            graphics.Transform = replacementWorldTransform;
            Assert.Equal(replacementWorldTransform.Value, graphics.Transform.Value);
            graphics.FillRectangle(brush, 1f, 2f, 3f, 4f);
        }

        Assert.Collection(
            context.Commands,
            command => Assert.Equal(new Rect(23f, 40f, 6f, 12f), command.Rect),
            command => Assert.Equal(new Rect(31f, 58f, 6f, 12f), command.Rect));
    }
}
