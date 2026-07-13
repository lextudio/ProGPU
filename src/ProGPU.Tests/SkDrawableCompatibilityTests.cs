using ProGPU.Scene;
using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkDrawableCompatibilityTests
{
    [Fact]
    public void DrawableReportsManagedMetadataAndTracksGeneration()
    {
        using var drawable = new TestDrawable();

        Assert.Equal(typeof(SKObject), typeof(SKDrawable).BaseType);
        Assert.Equal(new SKRect(1f, 2f, 11f, 12f), drawable.Bounds);
        Assert.Equal(64, drawable.ApproximateBytesUsed);
        var generation = drawable.GenerationId;
        drawable.NotifyDrawingChanged();
        Assert.NotEqual(generation, drawable.GenerationId);
    }

    [Fact]
    public void DrawablePlaybackComposesMatrixAndRestoresCanvasState()
    {
        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 64f, 64f);
        using var drawable = new TestDrawable();
        canvas.Translate(3f, 4f);

        canvas.DrawDrawable(drawable, 5f, 6f);
        canvas.DrawDrawable(drawable, new SKPoint(7f, 8f));

        Assert.Equal(2, context.Commands.Count);
        Assert.Equal(8f, context.Commands[0].Transform.M41);
        Assert.Equal(10f, context.Commands[0].Transform.M42);
        Assert.Equal(10f, context.Commands[1].Transform.M41);
        Assert.Equal(12f, context.Commands[1].Transform.M42);
        Assert.Equal(1, canvas.SaveCount);
    }

    [Fact]
    public void DefaultSnapshotRecordsDrawableIntoPicture()
    {
        using var drawable = new TestDrawable();
        using var picture = drawable.Snapshot();
        Assert.Equal(drawable.Bounds, picture.CullRect);

        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 64f, 64f);
        picture.Playback(canvas);

        var command = Assert.Single(context.Commands);
        Assert.Equal(RenderCommandType.DrawPicture, command.Type);
        Assert.NotNull(command.Picture);
    }

    [Fact]
    public void PictureRecorderCanFinishAsDrawable()
    {
        using var recorder = new SKPictureRecorder();
        var recordingCanvas = recorder.BeginRecording(new SKRect(0f, 0f, 20f, 30f));
        using (var paint = new SKPaint { Color = SKColors.Blue })
        {
            recordingCanvas.DrawRect(new SKRect(0f, 0f, 10f, 10f), paint);
        }

        using var drawable = recorder.EndRecordingAsDrawable();
        Assert.Equal(new SKRect(0f, 0f, 20f, 30f), drawable.Bounds);

        var context = new DrawingContext();
        using var canvas = new SKCanvas(context, 20f, 30f);
        canvas.DrawDrawable(drawable, 0f, 0f);
        var command = Assert.Single(context.Commands);
        Assert.Equal(RenderCommandType.DrawPicture, command.Type);
    }

    private sealed class TestDrawable : SKDrawable
    {
        protected internal override void OnDraw(SKCanvas canvas)
        {
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(new SKRect(1f, 2f, 11f, 12f), paint);
        }

        protected internal override int OnGetApproximateBytesUsed() => 64;
        protected internal override SKRect OnGetBounds() => new(1f, 2f, 11f, 12f);
    }
}
