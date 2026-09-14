using ProGPU.GameEngine.Assets;

namespace ProGPU.Samples.Suntrail.Game.Import;

public readonly record struct SmbxArtworkIssue(SmbxArtworkKind Kind, int Id, string Message);

/// <summary>
/// A source document and prepared CPU artwork from a caller-owned package. Preparation
/// visits O(R + A + P) records R, distinct requested assets A and decoded pixels P,
/// within the document/package/pixel limits. Drawing never parses or decodes here.
/// Missing or unsupported art is reported independently of source preservation.
/// </summary>
public sealed class SmbxLevelPack
{
    private readonly Dictionary<(SmbxArtworkKind Kind, int Id), SmbxArtwork> _artwork;
    private readonly SmbxArtworkIssue[] _issues;
    private readonly Dictionary<(SmbxArtworkKind Kind, int Id), SmbxObjectDefinition> _definitions;
    public SmbxSourceDocument Document { get; }
    public ReadOnlySpan<SmbxArtworkIssue> Issues => _issues;
    public int DecodedBytes { get; }
    private SmbxLevelPack(SmbxSourceDocument document,
        Dictionary<(SmbxArtworkKind, int), SmbxArtwork> artwork, SmbxArtworkIssue[] issues, int decodedBytes,
        Dictionary<(SmbxArtworkKind, int), SmbxObjectDefinition> definitions)
    { Document = document; _artwork = artwork; _issues = issues; DecodedBytes = decodedBytes; _definitions = definitions; }
    public bool TryGetArtwork(SmbxArtworkKind kind, int id, out SmbxArtwork artwork) => _artwork.TryGetValue((kind, id), out artwork!);
    public bool TryGetDefinition(SmbxArtworkKind kind, int id, out SmbxObjectDefinition definition) => _definitions.TryGetValue((kind, id), out definition!);

    public static string[] LevelPaths(AssetBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return bundle.Paths.Where(p => p.EndsWith(".lvl", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".lvlx", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(p).StartsWith("._", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
    }

    public static SmbxLevelPack Open(AssetBundle bundle, string levelPath, SmbxConfigPack? definitions = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        levelPath = AssetBundle.ResolvePath("", levelPath);
        var bytes = bundle.Get(levelPath);
        var document = levelPath.EndsWith(".lvlx", StringComparison.OrdinalIgnoreCase)
            ? SmbxSourceDocument.ReadLvlx(bytes.Span, levelPath)
            : levelPath.EndsWith(".lvl", StringComparison.OrdinalIgnoreCase)
                ? SmbxSourceDocument.ReadLegacy(bytes.Span, levelPath)
                : throw new FormatException("Select an LVL or LVLX entry from the package.");
        return Prepare(bundle, document, definitions);
    }

    public static SmbxLevelPack Prepare(AssetBundle bundle, SmbxSourceDocument document, SmbxConfigPack? definitions = null)
    {
        ArgumentNullException.ThrowIfNull(bundle); ArgumentNullException.ThrowIfNull(document);
        var catalog = new SmbxArtworkCatalog(bundle, document.FileName, definitions);
        var wanted = new HashSet<(SmbxArtworkKind Kind, int Id)>();
        var artwork = new Dictionary<(SmbxArtworkKind, int), SmbxArtwork>();
        var preparedDefinitions = new Dictionary<(SmbxArtworkKind, int), SmbxObjectDefinition>();
        var issues = new List<SmbxArtworkIssue>();
        foreach (var record in document.Records)
        {
            if (record.SectionPath != record.Section) continue;
            SmbxArtworkKind? kind = record.Section switch
            {
                "BLOCK" => SmbxArtworkKind.Block, "BGO" => SmbxArtworkKind.Background,
                "NPC" => SmbxArtworkKind.Npc, _ => null
            };
            if (kind is null) continue;
            int id;
            try { id = record.Get("ID").GetInteger(); }
            catch (FormatException error) { issues.Add(new(kind.Value, 0, error.Message)); continue; }
            var key = (kind.Value, id);
            if (!wanted.Add(key)) continue;
            if (id <= 0) { issues.Add(new(kind.Value, id, "Artwork requires a positive source ID.")); continue; }
            try
            {
                var asset = catalog.Prepare(kind.Value, id);
                if (asset is null) issues.Add(new(kind.Value, id, "No image supplied for this ID in the level, episode or selected base configuration."));
                else
                {
                    artwork.Add(key, asset);
                    // Retain only definitions for prepared images (at most 128).
                    // Invalid metadata is an issue without discarding valid pixels.
                    if (definitions?.Get(kind.Value, id) is { } definition) preparedDefinitions.Add(key, definition);
                }
            }
            catch (FormatException error) { issues.Add(new(kind.Value, id, error.Message)); }
        }
        return new(document, artwork, issues.ToArray(), catalog.DecodedBytes, preparedDefinitions);
    }
}
