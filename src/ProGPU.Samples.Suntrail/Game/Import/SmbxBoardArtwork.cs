using System.Globalization;
using ProGPU.GameEngine.Assets;

namespace ProGPU.Samples.Suntrail.Game.Import;

/// <summary>Prepared first-frame placement relative to an editable source anchor, without runtime collision semantics.</summary>
public sealed record SmbxPlacedArtwork(RasterImage Image, SmbxBounds Frame, SmbxBounds OffsetBounds)
{
    public bool TiledNineSlice { get; init; }
}

/// <summary>
/// CPU editor placement prepared once per immutable source/art change. O(N) time
/// and storage for N geometry items. No image reads, decoding or GPU calls. Unknown
/// layouts retain their source markers and report why placement is unresolved.
/// </summary>
public sealed class SmbxBoardArtwork
{
    private readonly SmbxPlacedArtwork?[] _items;
    private readonly SmbxGeometryIssue[] _issues;
    public ReadOnlySpan<SmbxGeometryIssue> Issues => _issues;
    public int Count { get; }
    public SmbxPlacedArtwork? this[int geometryIndex] => _items[geometryIndex];

    public SmbxBoardArtwork(SmbxGeometryEditor editor, SmbxLevelPack pack)
    {
        ArgumentNullException.ThrowIfNull(editor); ArgumentNullException.ThrowIfNull(pack);
        _items = new SmbxPlacedArtwork?[editor.Geometry.Length]; var issues = new List<SmbxGeometryIssue>();
        for (int i = 0; i < _items.Length; i++)
        {
            var item = editor.Geometry[i];
            SmbxArtworkKind? kind = item.Kind switch
            {
                SmbxGeometryKind.Block => SmbxArtworkKind.Block,
                SmbxGeometryKind.BackgroundAnchor => SmbxArtworkKind.Background,
                SmbxGeometryKind.NpcAnchor => SmbxArtworkKind.Npc, _ => null
            };
            if (kind is null || !int.TryParse(item.SourceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ||
                !pack.TryGetArtwork(kind.Value, id, out var art)) continue;
            pack.TryGetDefinition(kind.Value, id, out var definition);
            try
            {
                if (kind == SmbxArtworkKind.Npc)
                {
                    var sheet = art.NpcGraphics.ResolveSheet(art.Image) ?? throw new FormatException("NPC board placement needs an explicit supported frame layout.");
                    int? width = art.NpcGraphics.Integer("width") ?? definition?.PhysicalWidth;
                    int? height = art.NpcGraphics.Integer("height") ?? definition?.PhysicalHeight;
                    if (width is null || height is null || width is <= 0 or > 8192 || height is <= 0 or > 8192)
                        throw new FormatException("NPC board placement needs positive supplied body dimensions (width and height).");
                    var row = editor.Document.Records[item.Record];
                    bool right = row.TryGet("D", out var direction) && direction.GetInteger() > 0;
                    var frame = sheet.Frame(0, right);
                    // Editor preview attaches the graphic to the body's bottom center.
                    // Physical dimensions are placement metadata, not new collision data.
                    _items[i] = new(art.Image, new(frame.X, frame.Y, frame.Width, frame.Height),
                        new((width.Value - sheet.Width) / 2d + sheet.OffsetX, height.Value - sheet.Height + sheet.OffsetY, sheet.Width, sheet.Height));
                }
                else
                {
                    var fields = definition?.Fields ?? throw new FormatException("Block/BGO board placement needs a supplied animation definition.");
                    if (kind == SmbxArtworkKind.Block && fields.Boolean("sizable") == true)
                    {
                        if (art.Image.Width != 96 || art.Image.Height != 96 || fields.Boolean("animated") == true)
                            throw new FormatException("Sizable block placement currently requires a static 96 × 96 nine-part image.");
                        if (item.Bounds.Width is < 64 or > 65_536 || item.Bounds.Height is < 64 or > 65_536)
                            throw new FormatException("Sizable block preview currently supports dimensions from 64 to 65,536 source pixels.");
                        _items[i] = new(art.Image, new(0, 0, 96, 96), new(0, 0, item.Bounds.Width, item.Bounds.Height)) { TiledNineSlice = true };
                        Count++; continue;
                    }
                    bool? animated = fields.Boolean("animated");
                    int? frames = animated == false ? 1 : fields.Integer("frames");
                    if (frames is null || frames is <= 0 or > 4096 || art.Image.Height % frames.Value != 0)
                        throw new FormatException("Board placement needs an explicit static image or a supported vertical frame count.");
                    int height = art.Image.Height / frames.Value;
                    if (kind == SmbxArtworkKind.Block && (item.Bounds.Width != art.Image.Width || item.Bounds.Height != height))
                        throw new FormatException("This block's source size differs from its frame; resized/tiled placement needs its sizing rule.");
                    _items[i] = new(art.Image, new(0, 0, art.Image.Width, height), new(0, 0, art.Image.Width, height));
                }
                Count++;
            }
            catch (FormatException error) { issues.Add(new(item.Record, error.Message)); }
        }
        _issues = issues.ToArray();
    }

    public SmbxBounds Bounds(int index, SmbxBounds anchor)
    {
        var item = _items[index] ?? throw new ArgumentException("This object has no prepared sprite.", nameof(index));
        return item.OffsetBounds with { X = anchor.X + item.OffsetBounds.X, Y = anchor.Y + item.OffsetBounds.Y };
    }
}
