namespace SkiaSharp;

public class SKDrawable : SKObject
{
    private int _generationId = 1;

    public uint GenerationId => unchecked((uint)Volatile.Read(ref _generationId));
    public SKRect Bounds => OnGetBounds();
    public int ApproximateBytesUsed => OnGetApproximateBytesUsed();

    protected SKDrawable()
        : this(owns: true)
    {
    }

    protected SKDrawable(bool owns)
        : base(SKObjectHandle.Create(), owns)
    {
    }

    public void Draw(SKCanvas canvas, in SKMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.Save();
        try
        {
            canvas.Concat(matrix);
            OnDraw(canvas);
        }
        finally
        {
            canvas.Restore();
        }
    }

    public void Draw(SKCanvas canvas, float x, float y) =>
        Draw(canvas, SKMatrix.CreateTranslation(x, y));

    public SKPicture Snapshot() => OnSnapshot();

    public void NotifyDrawingChanged()
    {
        if (Interlocked.Increment(ref _generationId) == 0)
        {
            Interlocked.CompareExchange(ref _generationId, 1, 0);
        }
    }

    protected internal virtual void OnDraw(SKCanvas canvas)
    {
    }

    protected internal virtual int OnGetApproximateBytesUsed() => 0;
    protected internal virtual SKRect OnGetBounds() => SKRect.Empty;

    protected internal virtual SKPicture OnSnapshot()
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(Bounds);
        Draw(canvas, 0f, 0f);
        return recorder.EndRecording();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}

internal sealed class SKRecordedPictureDrawable : SKDrawable
{
    private SKPicture? _picture;

    public SKRecordedPictureDrawable(SKPicture picture)
    {
        _picture = picture;
    }

    protected internal override void OnDraw(SKCanvas canvas)
    {
        canvas.DrawPicture(_picture ?? throw new ObjectDisposedException(nameof(SKRecordedPictureDrawable)));
    }

    protected internal override SKRect OnGetBounds() =>
        _picture?.CullRect ?? SKRect.Empty;

    protected override void DisposeManaged()
    {
        _picture?.Dispose();
        _picture = null;
        base.DisposeManaged();
    }
}
