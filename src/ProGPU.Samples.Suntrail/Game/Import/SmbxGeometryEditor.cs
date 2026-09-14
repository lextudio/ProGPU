using System.Globalization;

namespace ProGPU.Samples.Suntrail.Game.Import;

public enum SmbxGeometryKind { Section, Block, BackgroundAnchor, NpcAnchor, PlayerAnchor, WarpEntrance, WarpExit, Physics }

/// <summary>Global source coordinates. Subtract the editor camera in double precision before drawing.</summary>
public readonly record struct SmbxBounds(double X, double Y, double Width, double Height);
public readonly record struct SmbxGeometry(int Record, SmbxGeometryKind Kind, SmbxBounds Bounds, string SourceId, bool IsAnchor);
public readonly record struct SmbxGeometryIssue(int Record, string Message);

/// <summary>
/// Source-preserving geometry transactions for the documented LVL/LVLX coordinate fields.
/// Geometry is an editor view, not collision, artwork or runtime behavior substitution.
/// Loading/committing is O(B + F); pointer motion is O(1), allocation-free. Hit testing
/// is O(N), with N bounded by the source record limit. History stores field deltas,
/// never whole eight-MiB document snapshots; at most 64 actions and four MiB of UTF-16
/// field/name text. Only the current immutable source and its geometry view are retained.
/// </summary>
public sealed partial class SmbxGeometryEditor
{
    // Above this range a later float viewport projection needs a different camera
    // representation. Preserve such source records but leave them uneditable here.
    public const double CoordinateLimit = 1L << 40;
    private const int HistoryByteLimit = 4 * 1024 * 1024;
    private sealed record Edit(SmbxFieldEdit[] Before, SmbxFieldEdit[] After, int Bytes,
        int BeforeSelected, int AfterSelected, SmbxSourceSplice? Splice = null);
    private readonly List<Edit> _undo = [], _redo = [];
    private int _historyBytes;
    private SmbxGeometry[] _geometry = [];
    private SmbxGeometryIssue[] _issues = [];
    private SmbxGeometry? _moving;
    private SmbxBounds _preview;

    public SmbxSourceDocument Document { get; private set; }
    public ReadOnlySpan<SmbxGeometry> Geometry => _geometry;
    public ReadOnlySpan<SmbxGeometryIssue> Issues => _issues;
    public int Selected { get; private set; } = -1;
    public int Revision { get; private set; }
    public bool IsMoving => _moving.HasValue;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event Action? Changed;

    public SmbxGeometryEditor(SmbxSourceDocument document)
    { ArgumentNullException.ThrowIfNull(document); Document = document; Rebuild(); }

    public SmbxBounds Bounds(int index) => _moving.HasValue && index == Selected ? _preview : _geometry[index].Bounds;
    public void Select(int index)
    { CancelMove(); Selected = (uint)index < _geometry.Length ? index : -1; Changed?.Invoke(); }

    /// <param name="anchorRadius">World-space selection radius corresponding to a fixed screen-size anchor marker.</param>
    public int HitTest(double x, double y, double anchorRadius)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(anchorRadius) || anchorRadius <= 0) return -1;
        // Sections form the canvas backdrop; pick smaller objects before section frames.
        for (int pass = 0; pass < 2; pass++)
            for (int i = _geometry.Length - 1; i >= 0; i--)
            {
                var item = _geometry[i]; if ((item.Kind == SmbxGeometryKind.Section) != (pass == 1)) continue;
                var b = Bounds(i);
                if (item.IsAnchor)
                { if (Math.Abs(x - b.X) <= anchorRadius && Math.Abs(y - b.Y) <= anchorRadius) return i; }
                else if (x >= b.X && x <= b.X + b.Width && y >= b.Y && y <= b.Y + b.Height) return i;
            }
        return -1;
    }

    public void BeginMove(int index)
    { Select(index); if (Selected >= 0) { _moving = _geometry[Selected]; _preview = _moving.Value.Bounds; } }

    /// <summary>Delta from the original press, snapped as a delta to preserve off-grid source placement.</summary>
    public void Move(double dx, double dy, double grid = 16)
    {
        if (_moving is not { } original) return;
        if (!double.IsFinite(dx) || !double.IsFinite(dy) || !double.IsFinite(grid) || grid < 1 || grid > 65536)
            throw new ArgumentOutOfRangeException(nameof(dx));
        // LVLX coordinate fields are integers. Legacy numeric coordinates can keep
        // their existing fractional offset while the editor applies integral steps.
        if (grid != Math.Truncate(grid)) throw new ArgumentOutOfRangeException(nameof(grid));
        var candidate = original.Bounds with
        {
            X = original.Bounds.X + Math.Round(dx / grid, MidpointRounding.AwayFromZero) * grid,
            Y = original.Bounds.Y + Math.Round(dy / grid, MidpointRounding.AwayFromZero) * grid
        };
        ValidateBounds(candidate, original.IsAnchor);
        if (candidate == _preview) return;
        _preview = candidate; Changed?.Invoke();
    }

    public void CancelMove()
    { if (_moving is null) return; _moving = null; Changed?.Invoke(); }

    public void CommitMove()
    {
        if (_moving is not { } original) return;
        if (original.Bounds == _preview) { CancelMove(); return; }
        var b = _preview;
        var fields = original.Kind switch
        {
            SmbxGeometryKind.Section => new[] { Field(original.Record, "L", b.X), Field(original.Record, "T", b.Y),
                Field(original.Record, "R", b.X + b.Width), Field(original.Record, "B", b.Y + b.Height) },
            SmbxGeometryKind.WarpEntrance => new[] { Field(original.Record, "IX", b.X), Field(original.Record, "IY", b.Y) },
            SmbxGeometryKind.WarpExit => new[] { Field(original.Record, "OX", b.X), Field(original.Record, "OY", b.Y) },
            _ => new[] { Field(original.Record, "X", b.X), Field(original.Record, "Y", b.Y) }
        };
        Apply(fields); // Parsing succeeds before the active drag/document changes.
    }

    public void Resize(double width, double height)
    {
        CancelMove(); if (Selected < 0) return;
        var item = _geometry[Selected];
        if (item.Kind is not (SmbxGeometryKind.Block or SmbxGeometryKind.Physics))
            throw new FormatException("This object has no editable rectangular size fields.");
        if (width != Math.Truncate(width) || height != Math.Truncate(height) || width > ushort.MaxValue || height > ushort.MaxValue)
            throw new FormatException("Editor sizes must be whole units from 1 through 65535.");
        ValidateBounds(item.Bounds with { Width = width, Height = height }, false);
        if (item.Bounds.Width == width && item.Bounds.Height == height) return;
        Apply([Field(item.Record, "W", width), Field(item.Record, "H", height)]);
    }

    private static SmbxFieldEdit Field(int record, string name, double value) => new(record, name, value.ToString("R", CultureInfo.InvariantCulture));

    public void SetLayer(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length > 1024) throw new FormatException("Editor layer names support at most 1024 characters.");
        if (Selected < 0) return;
        var item = _geometry[Selected];
        if (item.Kind is SmbxGeometryKind.Section or SmbxGeometryKind.PlayerAnchor)
            throw new FormatException("This source item has no object layer assignment.");
        var row = Document.Records[item.Record];
        if (row.TryGet("LR", out var current) && current.GetString() == name) return;
        SetProperty(item.Record, "LR", Document.IsLegacy ? SmbxSourceDocument.EncodeLegacyString(name) : SmbxSourceDocument.EncodeString(name));
    }

    public void SetNpcDirection(int direction)
    {
        if (direction is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(direction));
        if (Selected < 0) return;
        var item = _geometry[Selected];
        if (item.Kind != SmbxGeometryKind.NpcAnchor) throw new FormatException("Select an NPC to change its direction.");
        var row = Document.Records[item.Record];
        if (row.TryGet("D", out var current) && current.GetInteger() == direction) return;
        SetProperty(item.Record, "D", direction.ToString(CultureInfo.InvariantCulture));
    }

    private void SetProperty(int record, string field, string value)
    {
        if (Document.Records[record].TryGet(field, out _)) { Apply([new(record, field, value)]); return; }
        var splice = Document.InsertObjectField(record, field, value);
        if (splice.HistoryBytes > HistoryByteLimit) throw new FormatException("This property exceeds the undo budget.");
        var next = Document.WithSplice(splice); var projected = Project(next);
        Remember(new([], [], splice.HistoryBytes, Selected, Selected, splice)); Publish(next, projected);
    }

    public static SmbxBounds PaletteBounds(SmbxGeometryKind kind, double x, double y) => kind switch
    {
        SmbxGeometryKind.Block => new(x, y, 96, 32), SmbxGeometryKind.Physics => new(x, y, 160, 96),
        SmbxGeometryKind.WarpEntrance => new(x, y, 160, 0),
        SmbxGeometryKind.BackgroundAnchor or SmbxGeometryKind.NpcAnchor => new(x, y, 0, 0),
        _ => throw new FormatException("This kind is not a palette object.")
    };

    public void Add(SmbxGeometryKind kind, int id, double x, double y)
    {
        CancelMove();
        var bounds = PaletteBounds(kind, Math.Round(x / 16, MidpointRounding.AwayFromZero) * 16, Math.Round(y / 16, MidpointRounding.AwayFromZero) * 16);
        ValidateBounds(bounds, kind is not (SmbxGeometryKind.Block or SmbxGeometryKind.Physics));
        ApplyStructure(Document.InsertGeometryRecord(kind, id, bounds), true);
    }

    public void DeleteSelected()
    {
        CancelMove(); if (Selected < 0) return;
        ApplyStructure(Document.DeleteGeometryRecord(_geometry[Selected].Record), false);
    }

    private void ApplyStructure(SmbxSourceSplice splice, bool inserted)
    {
        if (splice.HistoryBytes > HistoryByteLimit) throw new FormatException("This object exceeds the undo text budget.");
        var next = Document.WithSplice(splice); var projected = Project(next); int selection = -1;
        if (inserted)
            for (int i = 0; i < projected.Item1.Length; i++)
            {
                int start = next.Records[projected.Item1[i].Record].Start;
                if (start >= splice.Offset && start < splice.Offset + splice.After.Length) { selection = i; break; }
            }
        Remember(new([], [], splice.HistoryBytes, Selected, selection, splice));
        Selected = selection; Publish(next, projected);
    }

    private void Apply(SmbxFieldEdit[] after)
    {
        var before = new SmbxFieldEdit[after.Length]; int bytes = 0;
        for (int i = 0; i < after.Length; i++)
        {
            var field = after[i]; before[i] = new(field.Record, field.Field, Document.Records[field.Record].Get(field.Field).RawValue);
            bytes = checked(bytes + (before[i].RawValue.Length + field.RawValue.Length + 2 * field.Field.Length) * sizeof(char));
        }
        if (bytes > HistoryByteLimit) throw new FormatException("This geometry change exceeds the undo budget.");
        var next = Document.WithFields(after);
        // Build the next projection transactionally as well as the source text.
        var projected = Project(next);
        Remember(new(before, after, bytes, Selected, Selected));
        Publish(next, projected);
    }

    private void Remember(Edit change)
    {
        foreach (var edit in _redo) _historyBytes -= edit.Bytes;
        _redo.Clear();
        while (_undo.Count > 0 && (_undo.Count == 64 || _historyBytes + change.Bytes > HistoryByteLimit))
        { _historyBytes -= _undo[0].Bytes; _undo.RemoveAt(0); }
        _undo.Add(change); _historyBytes += change.Bytes;
    }

    public void Undo() => Travel(_undo, _redo, false);
    public void Redo() => Travel(_redo, _undo, true);
    private void Travel(List<Edit> from, List<Edit> to, bool forward)
    {
        CancelMove(); if (from.Count == 0) return;
        var edit = from[^1];
        var next = edit.Splice is { } splice ? Document.WithSplice(forward ? splice : splice.Reverse()) : Document.WithFields(forward ? edit.After : edit.Before);
        var projected = Project(next);
        from.RemoveAt(from.Count - 1); to.Add(edit); Selected = forward ? edit.AfterSelected : edit.BeforeSelected; Publish(next, projected);
    }
    private void Publish(SmbxSourceDocument document, (SmbxGeometry[], SmbxGeometryIssue[]) projected)
    {
        Document = document; (_geometry, _issues) = projected; _moving = null;
        Selected = Math.Min(Selected, _geometry.Length - 1); Revision++; Changed?.Invoke();
    }
    private void Rebuild() => (_geometry, _issues) = Project(Document);

    private static (SmbxGeometry[], SmbxGeometryIssue[]) Project(SmbxSourceDocument document)
    {
        var geometry = new List<SmbxGeometry>(); var issues = new List<SmbxGeometryIssue>();
        for (int i = 0; i < document.Records.Length; i++)
        {
            var row = document.Records[i]; if (row.SectionPath != row.Section) continue;
            if (row.Section is not ("SECTION" or "BLOCK" or "BGO" or "NPC" or "STARTPOINT" or "DOORS" or "PHYSICS")) continue;
            try
            {
                double Number(string field)
                {
                    var source = row.Get(field); double value = source.GetNumber();
                    if (!document.IsLegacy && (!long.TryParse(source.RawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer) || integer != value))
                        throw new FormatException($"'{field}' must be an integer source coordinate.");
                    return value;
                }
                string id = row.Section != "DOORS" && row.TryGet("ID", out var identifier) ? identifier.RawValue : "";
                void Add(SmbxGeometryKind kind, SmbxBounds bounds, bool anchor = false)
                { ValidateBounds(bounds, anchor); geometry.Add(new(i, kind, bounds, id, anchor)); }
                switch (row.Section)
                {
                    case "SECTION":
                        double left = Number("L"), top = Number("T"), right = Number("R"), bottom = Number("B");
                        if (left == right && top == bottom) break; // Unused legacy section slot.
                        Add(SmbxGeometryKind.Section, new(left, top, right - left, bottom - top)); break;
                    case "BLOCK": Add(SmbxGeometryKind.Block, new(Number("X"), Number("Y"), Number("W"), Number("H"))); break;
                    case "PHYSICS":
                        if (Number("H") == -1) throw new FormatException("Circular physics zones are preserved; geometry editing needs circle support.");
                        Add(SmbxGeometryKind.Physics, new(Number("X"), Number("Y"), Number("W"), Number("H"))); break;
                    case "DOORS":
                        // Endpoint lengths/directions remain in the source. These
                        // markers are anchors, never invented warp collision boxes.
                        Add(SmbxGeometryKind.WarpEntrance, new(Number("IX"), Number("IY"), 0, 0), true);
                        Add(SmbxGeometryKind.WarpExit, new(Number("OX"), Number("OY"), 0, 0), true); break;
                    default:
                        if (document.IsLegacy && row.Section == "STARTPOINT" && Number("W") == 0 && Number("H") == 0) break;
                        var kind = row.Section == "BGO" ? SmbxGeometryKind.BackgroundAnchor : row.Section == "NPC" ? SmbxGeometryKind.NpcAnchor : SmbxGeometryKind.PlayerAnchor;
                        Add(kind, new(Number("X"), Number("Y"), 0, 0), true); break;
                }
            }
            catch (FormatException e) { issues.Add(new(i, e.Message)); }
        }
        return (geometry.ToArray(), issues.ToArray());
    }

    private static void ValidateBounds(SmbxBounds b, bool anchor)
    {
        if (!double.IsFinite(b.X) || !double.IsFinite(b.Y) || !double.IsFinite(b.Width) || !double.IsFinite(b.Height) ||
            Math.Abs(b.X) > CoordinateLimit || Math.Abs(b.Y) > CoordinateLimit ||
            Math.Abs(b.X + b.Width) > CoordinateLimit || Math.Abs(b.Y + b.Height) > CoordinateLimit ||
            (!anchor && (b.Width <= 0 || b.Height <= 0)))
            throw new FormatException("Geometry is outside the editor coordinate range or has a nonpositive size; its source is preserved.");
    }
}
