using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProGPU.Scene;
using ProGPU.Vector;
using ProGPU.Samples.Suntrail.Game.Import;
using ProGPU.Samples.Suntrail.Rendering;

namespace ProGPU.Samples.Suntrail.Presentation;

/// <summary>Prepared CPU-art inspection. Metadata changes outside rendering; draw commands are immutable.</summary>
public sealed class SmbxArtworkPreview : Control
{
    private SmbxArtwork? _artwork;
    private SmbxNpcSheet? _sheet;
    private bool _right;
    private int _frame;
    public string Description { get; private set; } = "Select an object\nto inspect its artwork.";
    public SmbxArtwork? Artwork => _artwork;
    public Rect Source => _artwork is null ? default : _sheet is null
        ? new Rect(0, 0, _artwork.Image.Width, _artwork.Image.Height)
        : new Rect(0, _sheet.Frame(_frame, _right).Y, _sheet.Width, _sheet.Height);
    public SmbxArtworkPreview()
    { Name = "SmbxArtworkPreview"; Height = 112; Background = new ThemeResourceBrush("SuntrailInk"); }
    public void SetArtwork(SmbxArtwork? artwork, bool right)
    {
        if (ReferenceEquals(_artwork, artwork) && _right == right) return;
        _artwork = artwork; _right = right; _sheet = null; _frame = 0;
        if (artwork is null) Description = "No supplied image\nfor this object.";
        else
        {
            try { _sheet = artwork.NpcGraphics.ResolveSheet(artwork.Image); Describe(); }
            catch (FormatException error) { Description = "Whole sheet\n" + error.Message; }
        }
        Invalidate();
    }
    public void NextFrame()
    {
        if (_sheet is null) return;
        _frame = (_frame + 1) % _sheet.Frames; Describe(); Invalidate();
    }
    private void Describe() => Description = _sheet is null ? "Whole source sheet\nFrame layout unknown." : $"Frame {_frame + 1}/{_sheet.Frames}\n{(_right ? "Right" : "Left")} preview";
    public override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(GetCurrentBackground(), null, new Rect(0, 0, Size.X, Size.Y));
        if (_artwork is null || Size.X <= 8 || Size.Y <= 8) return;
        var source = Source; float scale = MathF.Min((Size.X - 8) / source.Width, (Size.Y - 8) / source.Height);
        float width = source.Width * scale, height = source.Height * scale;
        context.DrawArtwork(new(this, _artwork.Image, new Rect((Size.X - width) / 2, (Size.Y - height) / 2, width, height), source));
    }
}
