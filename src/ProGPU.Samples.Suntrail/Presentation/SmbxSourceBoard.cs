using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using ProGPU.Scene;
using ProGPU.Vector;
using ProGPU.Samples.Suntrail.Game.Import;
using ProGPU.Samples.Suntrail.Rendering;

namespace ProGPU.Samples.Suntrail.Presentation;

/// <summary>
/// Retained source geometry with an immutable spatial hierarchy. Source/art changes
/// prepare O(N log N) data; drawing/picking query candidate items in authored order,
/// with no per-item control tree. Camera-only artwork reuses an overscanned batch.
/// Coordinates rebase in double precision before projection to logical pixels.
/// Prepared supported sprite layouts draw behind editing overlays. Actor/warp
/// symbols denote source anchors, not invented runtime collision bounds.
/// </summary>
public sealed partial class SmbxSourceBoard : Control
{
    private SmbxGeometryEditor? _editor;
    private SmbxLevelPack? _artworkPack;
    private SmbxSourceDocument? _spriteDocument;
    private readonly List<RasterArtworkSprite> _visibleSprites = [];
    private RasterArtworkBatch? _retainedSprites;
    private bool _spritesValid;
    private double _spriteOriginX, _spriteOriginY, _spriteZoom;
    private Vector2 _spriteViewport;
    private bool _spriteMoveWasActive;
    public SmbxBoardArtwork? PreparedArtwork { get; private set; }
    public SmbxEditorLayers? Layers { get; private set; }
    private readonly Pen _line, _selected;
    private uint? _pointer;
    private Vector2 _press;
    private double _pressX, _pressY;
    private bool _panning;
    private int _section = -1;
    private bool _placing;
    private Vector2? _placementPoint;
    public SmbxGeometryKind? Tool { get; private set; }
    public int PlacementId { get; private set; } = 1;
    public double CameraX { get; private set; }
    public double CameraY { get; private set; }
    public double Zoom { get; private set; } = .75;
    public bool PanMode { get; set; }
    public event Action<string>? Notice;

    public SmbxSourceBoard()
    {
        Name = "SmbxSourceMap"; AutomationProperties.SetName(this, "SMBX level geometry map");
        Background = new ThemeResourceBrush("SuntrailButton");
        Foreground = new ThemeResourceBrush("SuntrailCream"); BorderBrush = new ThemeResourceBrush("SuntrailGold");
        _line = new(Foreground!, 1); _selected = new(BorderBrush!, 3);
    }
    public void SetEditor(SmbxGeometryEditor? editor, SmbxLevelPack? artwork = null)
    {
        ClearPlaytest(); InvalidateSpriteSnapshot(); ClearSpatialGeneration(); _spriteMoveWasActive = false;
        Cancel(); if (_editor is { } old) old.Changed -= OnEditorChanged;
        _artworkPack = artwork; _spriteDocument = null; PreparedArtwork = null; Layers = editor is null ? null : new(editor); _visibleSprites.Clear();
        _editor = editor; if (editor is not null) editor.Changed += OnEditorChanged;
        RebuildArtwork(); _section = -1; Tool = null; Fit(); Invalidate();
    }
    public void SetArtwork(SmbxLevelPack? pack)
    {
        if (ReferenceEquals(_artworkPack, pack)) return;
        _artworkPack = pack; RebuildArtwork(); Invalidate();
    }
    private void OnEditorChanged()
    {
        bool documentChanged = !ReferenceEquals(_spriteDocument, _editor?.Document);
        bool moving = _editor?.IsMoving == true;
        if (documentChanged || moving || _spriteMoveWasActive) InvalidateSpriteSnapshot();
        _spriteMoveWasActive = moving;
        if (documentChanged)
        { if (_editor is { } editor) Layers = new(editor, Layers); RebuildArtwork(); }
        Invalidate();
    }
    private void RebuildArtwork()
    {
        InvalidateSpriteSnapshot(); ClearSpatialGeneration();
        _spriteDocument = _editor?.Document;
        PreparedArtwork = _editor is { } editor && _artworkPack is { } pack &&
            editor.Document.FileName == pack.Document.FileName ? new SmbxBoardArtwork(editor, pack) : null;
        if (_editor is { } current) PrepareSpatialGeneration(current);
    }

    /// <summary>Pick visible sprite geometry as well as source anchors; never changes source collision dimensions.</summary>
    public int HitTestObject(double x, double y)
    {
        if (_editor is not { } editor || !double.IsFinite(x) || !double.IsFinite(y)) return -1;
        double radius = 10 / Zoom;
        int candidateCount = QueryGeometryBounds(editor, new(x - radius, y - radius, radius * 2, radius * 2));
        // Draw and pick order both follow the source record order. Sections stay behind content.
        for (int candidate = candidateCount - 1; candidate >= 0; candidate--)
        {
            int i = _spatialIds[candidate];
            var item = editor.Geometry[i]; if (item.Kind == SmbxGeometryKind.Section || Layers?.IsVisible(i) == false) continue;
            var anchor = editor.Bounds(i);
            if (PreparedArtwork?[i] is not null)
            {
                var bounds = PreparedArtwork.Bounds(i, anchor);
                if (Contains(bounds, x, y)) return i;
            }
            if (item.IsAnchor)
            { if (Math.Abs(x - anchor.X) <= 10 / Zoom && Math.Abs(y - anchor.Y) <= 10 / Zoom) return i; }
            else if (Contains(anchor, x, y)) return i;
        }
        for (int candidate = candidateCount - 1; candidate >= 0; candidate--)
        {
            int i = _spatialIds[candidate];
            if (editor.Geometry[i].Kind == SmbxGeometryKind.Section && Layers?.IsVisible(i) != false && Contains(editor.Bounds(i), x, y)) return i;
        }
        return -1;
    }
    private static bool Contains(SmbxBounds bounds, double x, double y) => x >= bounds.X && x <= bounds.X + bounds.Width &&
        y >= bounds.Y && y <= bounds.Y + bounds.Height;

    public void SetLayerVisible(int layerIndex, bool visible)
    {
        if (Layers is not { } layers) return;
        Cancel(); layers.SetVisible(layerIndex, visible); InvalidateSpriteSnapshot();
        if (_editor is { Selected: >= 0 } editor && !layers.IsVisible(editor.Selected)) editor.Select(-1);
        Invalidate();
    }
    public void ShowAllLayers() { Cancel(); Layers?.ShowAll(); InvalidateSpriteSnapshot(); Invalidate(); }

    public void SetTool(SmbxGeometryKind? tool, int id = 1)
    {
        if (id <= 0) throw new FormatException("Object ID must be a positive integer.");
        if (tool is { } kind) SmbxGeometryEditor.PaletteBounds(kind, 0, 0);
        Cancel(); Tool = tool; PlacementId = id; PanMode = false;
        Notice?.Invoke(tool is { } value ? $"Place {value}, ID {id}: drag from the palette or tap the map." : "Select and drag an object to move it.");
    }
    public void BeginPlacement(SmbxGeometryKind tool, int id, PointerRoutedEventArgs e)
    {
        if (!IsEnabled || _editor is null || _pointer.HasValue) return;
        SetTool(tool, id); _pointer = e.Pointer.PointerId; _placing = true;
        CapturePointer(e.Pointer);
    }
    protected override Vector2 MeasureOverride(Vector2 availableSize) => new(
        float.IsFinite(availableSize.X) ? availableSize.X : 900, float.IsFinite(availableSize.Y) ? availableSize.Y : 500);

    public (double X, double Y) SourcePoint(Vector2 point) => (CameraX + (point.X - Size.X / 2) / Zoom, CameraY + (point.Y - Size.Y / 2) / Zoom);
    public Vector2 ScreenPoint(double x, double y) => new((float)((x - CameraX) * Zoom + Size.X / 2), (float)((y - CameraY) * Zoom + Size.Y / 2));
    public void Pan(double pixelsX, double pixelsY)
    { Cancel(); SetCamera(CameraX + pixelsX / Zoom, CameraY + pixelsY / Zoom); }
    private void SetCamera(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;
        CameraX = Math.Clamp(x, -SmbxGeometryEditor.CoordinateLimit, SmbxGeometryEditor.CoordinateLimit);
        CameraY = Math.Clamp(y, -SmbxGeometryEditor.CoordinateLimit, SmbxGeometryEditor.CoordinateLimit); Invalidate();
    }
    public void ChangeZoom(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0) return;
        Cancel(); Zoom = Math.Clamp(Zoom * factor, .002, 8); Invalidate();
    }
    public void Fit()
    {
        Cancel(); if (_editor is not { } editor || editor.Geometry.IsEmpty) return;
        int index = editor.Selected;
        if (index < 0)
        {
            index = 0;
            for (int i = 0; i < editor.Geometry.Length; i++)
                if (editor.Geometry[i].Kind == SmbxGeometryKind.Section) { index = i; break; }
        }
        Frame(editor.Geometry[index].Bounds);
    }
    public void NextSection()
    {
        Cancel(); if (_editor is not { } editor) return;
        for (int step = 1; step <= editor.Geometry.Length; step++)
        {
            int index = (_section + step) % editor.Geometry.Length;
            if (editor.Geometry[index].Kind != SmbxGeometryKind.Section) continue;
            _section = index; Frame(editor.Geometry[index].Bounds); return;
        }
    }
    private void Frame(SmbxBounds b)
    {
        Zoom = Math.Clamp(Math.Min(Math.Max(Size.X - 64, 400) / Math.Max(b.Width, 320), Math.Max(Size.Y - 64, 200) / Math.Max(b.Height, 160)), .002, 8);
        SetCamera(b.X + b.Width / 2, b.Y + b.Height / 2);
    }
    public override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(GetCurrentBackground(), null, new Rect(0, 0, Size.X, Size.Y));
        if (_editor is not { } editor || Size.X <= 0 || Size.Y <= 0) return;
        _line.Brush = GetCurrentForeground() ?? _line.Brush; _selected.Brush = GetCurrentBorderBrush() ?? _selected.Brush;
        context.PushClip(new Rect(0, 0, Size.X, Size.Y));
        RecordSprites(context, editor);
        int visibleGeometryCount = QueryGeometry(editor, 20);
        // Source geometry stays visible over the artwork as editing overlays. The
        // sprites themselves retain source record order until semantic layer rules
        // are supported; these overlays do not define runtime collision or z-order.
        for (int pass = 0; pass < 2; pass++)
            for (int visible = 0; visible < visibleGeometryCount; visible++)
            {
                int i = _spatialIds[visible];
                var item = editor.Geometry[i]; if (Layers?.IsVisible(i) == false) continue; if ((item.Kind == SmbxGeometryKind.Section) != (pass == 0)) continue;
                if (Playtest is not null && (item.Kind == SmbxGeometryKind.PlayerAnchor || PreparedArtwork?[i] is not null)) continue;
                var b = editor.Bounds(i);
                double x = (b.X - CameraX) * Zoom + Size.X / 2, y = (b.Y - CameraY) * Zoom + Size.Y / 2;
                double w = b.Width * Zoom, h = b.Height * Zoom;
                if (item.IsAnchor) { x -= 7; y -= 7; w = h = 14; }
                if (x + w < -4 || y + h < -4 || x > Size.X + 4 || y > Size.Y + 4) continue;
                var pen = i == editor.Selected ? _selected : _line;
                // Clamp distant edges before float conversion. This is a geometry
                // overview; clipping cannot manufacture editable source values.
                float left = (float)Math.Max(x, -8192), top = (float)Math.Max(y, -8192);
                float right = (float)Math.Min(x + w, Size.X + 8192), bottom = (float)Math.Min(y + h, Size.Y + 8192);
                if (item.Kind is SmbxGeometryKind.NpcAnchor or SmbxGeometryKind.PlayerAnchor)
                    context.DrawEllipse(null, pen, new(left + 7, top + 7), 7, 7);
                else context.DrawRectangle(Playtest is not null && item.Kind == SmbxGeometryKind.Block ? GetCurrentForeground() : null,
                    pen, new Rect(left, top, right - left, bottom - top));
            }
        if (_placing && Tool is { } tool && _placementPoint is { } point)
        {
            var world = SourcePoint(point);
            double x = Math.Round(world.X / 16, MidpointRounding.AwayFromZero) * 16;
            double y = Math.Round(world.Y / 16, MidpointRounding.AwayFromZero) * 16;
            var b = SmbxGeometryEditor.PaletteBounds(tool, x, y); var location = ScreenPoint(x, y);
            if (tool is SmbxGeometryKind.Block or SmbxGeometryKind.Physics)
                context.DrawRectangle(null, _selected, new Rect(location.X, location.Y, (float)(b.Width * Zoom), (float)(b.Height * Zoom)));
            else context.DrawEllipse(null, _selected, location, 8, 8);
        }
        if (Playtest is not null && _courierPreview.Count > 0) context.DrawProceduralWorld(_courierPreview);
        context.PopClip();
    }

    private void InvalidateSpriteSnapshot() { _spritesValid = false; _retainedSprites = null; }
    private void RecordSprites(DrawingContext context, SmbxGeometryEditor editor)
    {
        if (PreparedArtwork is not { } artwork) return;
        // The 192-pixel margin contains every view translated by at most 96 pixels.
        // Rebase in double precision when preparing; small camera changes only alter
        // the command transform. Edits/layers/art/zoom/resize invalidate the snapshot.
        bool reuse = _spritesValid && _spriteZoom == Zoom && _spriteViewport == Size &&
            Math.Abs((CameraX - _spriteOriginX) * Zoom) <= 96 && Math.Abs((CameraY - _spriteOriginY) * Zoom) <= 96;
        if (!reuse)
        {
            _visibleSprites.Clear(); _spriteOriginX = CameraX; _spriteOriginY = CameraY; _spriteZoom = Zoom; _spriteViewport = Size;
            int visibleCount = QueryGeometry(editor, 192);
            for (int visible = 0; visible < visibleCount; visible++)
            {
                int i = _spatialIds[visible];
                if (Layers?.IsVisible(i) == false || artwork[i] is not { } sprite) continue;
                var b = artwork.Bounds(i, editor.Bounds(i));
                double x = (b.X - CameraX) * Zoom + Size.X / 2, y = (b.Y - CameraY) * Zoom + Size.Y / 2;
                double width = b.Width * Zoom, height = b.Height * Zoom;
                if (x + width <= -192 || y + height <= -192 || x >= Size.X + 192 || y >= Size.Y + 192) continue;
                var frame = sprite.Frame;
                _visibleSprites.Add(new(sprite.Image, new((float)x, (float)y, (float)width, (float)height),
                    new((float)frame.X, (float)frame.Y, (float)frame.Width, (float)frame.Height))
                { NineSlice = sprite.TiledNineSlice ? new Vector4(32, 32, (float)Zoom, (float)Zoom) : Vector4.Zero });
            }
            _retainedSprites = _visibleSprites.Count > 0 ? new(this, CollectionsMarshal.AsSpan(_visibleSprites)) : null;
            _visibleSprites.Clear(); _spritesValid = true;
        }
        if (_retainedSprites is { } batch)
            context.DrawArtworkBatch(batch, Matrix4x4.CreateTranslation((float)((_spriteOriginX - CameraX) * Zoom),
                (float)((_spriteOriginY - CameraY) * Zoom), 0));
    }

    public override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        if (Playtest is not null) return;
        if (!IsEnabled || _pointer.HasValue || _editor is not { } editor) return;
        if (Tool is { } tool)
        {
            BeginPlacement(tool, PlacementId, e); _placementPoint = e.GetCurrentPoint(this).Position; Invalidate(); e.Handled = true; return;
        }
        _press = e.GetCurrentPoint(this).Position; var point = SourcePoint(_press);
        int selected = PanMode ? -1 : HitTestObject(point.X, point.Y);
        _pointer = e.Pointer.PointerId; _panning = PanMode || selected < 0; _pressX = CameraX; _pressY = CameraY;
        if (!_panning) editor.BeginMove(selected); else editor.Select(-1);
        CapturePointer(e.Pointer); e.Handled = true;
    }
    public override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        if (_pointer != e.Pointer.PointerId || _editor is not { } editor) return;
        if (_placing) { _placementPoint = e.GetCurrentPoint(this).Position; Invalidate(); e.Handled = true; return; }
        Vector2 delta = (Vector2)e.GetCurrentPoint(this).Position - _press;
        if (_panning) SetCamera(_pressX - delta.X / Zoom, _pressY - delta.Y / Zoom);
        else
        {
            try { editor.Move(delta.X / Zoom, delta.Y / Zoom); }
            catch (FormatException error) { Notice?.Invoke(error.Message); }
        }
        e.Handled = true;
    }
    public override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        if (_pointer != e.Pointer.PointerId) return;
        OnPointerMoved(e); var point = _placementPoint; bool placing = _placing;
        _pointer = null; _placing = false; _placementPoint = null; ReleasePointerCapture(e.Pointer); Invalidate();
        try
        {
            if (placing && point is { } p && Tool is { } tool && p.X >= 0 && p.Y >= 0 && p.X <= Size.X && p.Y <= Size.Y)
            { var world = SourcePoint(p); _editor?.Add(tool, PlacementId, world.X, world.Y); }
            else _editor?.CommitMove();
        }
        catch (FormatException error) { _editor?.CancelMove(); Notice?.Invoke(error.Message); }
        e.Handled = true;
    }
    public override void OnPointerCanceled(PointerRoutedEventArgs e) { if (_pointer == e.Pointer.PointerId) { Cancel(); e.Handled = true; } }
    public override void OnPointerCaptureLost(PointerRoutedEventArgs e) { if (_pointer == e.Pointer.PointerId) { Cancel(); e.Handled = true; } }
    public void Cancel() { _pointer = null; _placing = false; _placementPoint = null; _editor?.CancelMove(); ReleasePointerCaptures(); Invalidate(); }
}
