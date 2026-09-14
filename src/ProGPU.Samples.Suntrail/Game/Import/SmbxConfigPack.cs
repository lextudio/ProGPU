using System.Globalization;
using ProGPU.GameEngine.Assets;

namespace ProGPU.Samples.Suntrail.Game.Import;

public sealed record SmbxObjectDefinition(SmbxArtworkKind Kind, int Id, string SourcePath, SmbxConfigDocument Source, SmbxConfigSection Fields)
{
    public string? ImageName => Fields.String("image");
    public string? Name => Fields.String("name");
    public int? GraphicsWidth => Dimension("gfx-width");
    public int? GraphicsHeight => Dimension("gfx-height");
    public int? PhysicalWidth => Dimension("physical-width");
    public int? PhysicalHeight => Dimension("physical-height");
    private int? Dimension(string key)
    {
        int? value = Fields.Integer(key);
        if (value is <= 0 or > 8192) throw new FormatException($"{key} must be within 1–8192 in the current object-definition adapter.");
        return value;
    }
}

/// <summary>
/// Explicit caller-supplied Moondust split-config package. Preserves complete source
/// fields and resolves index paths, typed dimensions and caller-supplied images. It supplies
/// definitions, not a foreign engine's algorithms, default ID tables or scripts.
/// All operations are load-time. Indexing is O(E) entries, name lookup is O(1)
/// expected and definition parsing is O(B) source bytes, with bounded caches.
/// </summary>
public sealed class SmbxConfigPack : IDisposable
{
    private readonly IAssetSource _bundle;
    private readonly Dictionary<string, string> _assetPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string?> _graphicNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _ownsSource;
    private bool _disposed;
    private readonly Dictionary<SmbxArtworkKind, string> _directories = [];
    private readonly Dictionary<(SmbxArtworkKind, int), SmbxObjectDefinition?> _definitions = [];
    private readonly Queue<(SmbxArtworkKind, int)> _definitionOrder = new();
    private readonly Dictionary<(SmbxArtworkKind, int), string> _failures = [];
    private readonly Queue<(SmbxArtworkKind, int)> _failureOrder = new();
    public int CachedSourceBytes { get; private set; }
    public int CachedFieldCount { get; private set; }
    public SmbxConfigDocument Main { get; }
    public string MainPath { get; }
    public string? GraphicsDirectory { get; }
    public SmbxConfigPack(IAssetSource bundle, string mainPath, bool ownsSource = false)
    {
        ArgumentNullException.ThrowIfNull(bundle); _bundle = bundle; _ownsSource = ownsSource;
        foreach (string path in bundle.Paths)
        {
            if (_assetPaths.Count >= IndexedAssetArchive.MaximumEntries)
                throw new FormatException("Definition packages support at most 16,384 asset paths.");
            if (!_assetPaths.TryAdd(path, path))
                throw new FormatException($"Case-ambiguous definition asset '{path}'. Rename it for portable loading.");
        }
        MainPath = FindAsset(AssetBundle.ResolvePath("", mainPath)) ?? throw new FormatException("The definition package has no selected main.ini.");
        Main = SmbxConfigDocument.Read(bundle.Get(MainPath).Span); var main = Main.GetSection("main");
        if (main.Boolean("application-dir") == true)
            throw new FormatException("Application-relative configuration assets require an explicitly repackaged root; ambient application files are not loaded.");
        if (main.String("graphics-level") is { Length: > 0 } graphics) GraphicsDirectory = AssetBundle.ResolvePath(MainPath, graphics);
        if (GraphicsDirectory is { } root)
            foreach (string path in _assetPaths.Values)
                if (path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                {
                    string name = Path.GetFileName(path);
                    // Null marks an ambiguous basename. Exact relative paths still work.
                    if (!_graphicNames.TryAdd(name, path)) _graphicNames[name] = null;
                }
        LoadIndex(SmbxArtworkKind.Block, "lvl_blocks.ini", "blocks-main");
        LoadIndex(SmbxArtworkKind.Background, "lvl_bgo.ini", "background-main");
        LoadIndex(SmbxArtworkKind.Npc, "lvl_npc.ini", "npc-main");
    }
    private void LoadIndex(SmbxArtworkKind kind, string name, string sectionName)
    {
        string? path = FindAsset(AssetBundle.ResolvePath(MainPath, name));
        if (path is null) return;
        var bytes = _bundle.Get(path);
        var source = SmbxConfigDocument.Read(bytes.Span);
        string? directory = source.GetSection(sectionName).String("config-dir");
        if (string.IsNullOrWhiteSpace(directory)) throw new FormatException($"{name} requires a split config-dir; monolithic object tables are not implemented yet.");
        _directories.Add(kind, AssetBundle.ResolvePath(path, directory));
    }
    public SmbxObjectDefinition? Get(SmbxArtworkKind kind, int id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (kind is not (SmbxArtworkKind.Block or SmbxArtworkKind.Background or SmbxArtworkKind.Npc))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var key = (kind, id); if (_definitions.TryGetValue(key, out var known)) return known;
        if (_failures.TryGetValue(key, out string? failure)) throw new FormatException(failure);
        SmbxObjectDefinition? result = null;
        try
        {
            if (_directories.TryGetValue(kind, out string? directory))
            {
                string stem = kind == SmbxArtworkKind.Block ? "block" : kind == SmbxArtworkKind.Background ? "background" : "npc";
                string? path = FindAsset(AssetBundle.ResolvePath("", directory + "/" + stem + "-" + id.ToString(CultureInfo.InvariantCulture) + ".ini"));
                if (path is not null)
                {
                    var content = _bundle.Get(path);
                    var source = SmbxConfigDocument.Read(content.Span);
                    result = new(kind, id, path, source, source.GetSection(stem));
                }
            }
        }
        catch (FormatException error)
        {
            if (_failures.Count == 64) _failures.Remove(_failureOrder.Dequeue());
            _failures.Add(key, error.Message); _failureOrder.Enqueue(key); throw;
        }
        int bytes = result?.Source.ByteLength ?? 0, fields = result?.Source.FieldCount ?? 0;
        while (_definitions.Count == 4096 || CachedSourceBytes + bytes > 16 * 1024 * 1024 || CachedFieldCount + fields > 65_536)
        {
            var oldest = _definitionOrder.Dequeue(); var removed = _definitions[oldest];
            CachedSourceBytes -= removed?.Source.ByteLength ?? 0; CachedFieldCount -= removed?.Source.FieldCount ?? 0;
            _definitions.Remove(oldest);
        }
        CachedSourceBytes += bytes; CachedFieldCount += fields;
        _definitions.Add(key, result); _definitionOrder.Enqueue(key); return result;
    }

    // Preparation-only adapter policy: explicit paths are relative to graphics-level.
    // A bare image name may also resolve to one unique descendant in that root. This
    // supports supplied packs organized in object subfolders without scanning per ID
    // or guessing when two subfolders contain the same name. No ambient file access.
    internal string? ResolveImage(SmbxArtworkKind kind, int id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (kind == SmbxArtworkKind.SectionBackground) return null;
        string? name = Get(kind, id)?.ImageName;
        if (string.IsNullOrWhiteSpace(name) || GraphicsDirectory is not { } root) return null;
        string relative = AssetBundle.ResolvePath("", name);
        string path = AssetBundle.ResolvePath("", root + "/" + relative);
        if (FindAsset(path) is { } exact) return exact;
        if (relative.Contains('/')) return null;
        if (!_graphicNames.TryGetValue(relative, out string? match)) return null;
        return match ?? throw new FormatException($"Base image '{relative}' is ambiguous under '{root}'. Supply an explicit relative image path.");
    }

    internal string? FindAsset(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _assetPaths.GetValueOrDefault(path);
    }
    internal ReadOnlyMemory<byte> ReadAsset(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _bundle.Get(path);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_ownsSource && _bundle is IDisposable disposable) disposable.Dispose();
        _definitions.Clear(); _definitionOrder.Clear(); CachedSourceBytes = CachedFieldCount = 0; _disposed = true;
        _failures.Clear(); _failureOrder.Clear();
        _assetPaths.Clear(); _graphicNames.Clear();
    }
}
