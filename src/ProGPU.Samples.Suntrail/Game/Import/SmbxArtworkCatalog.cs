using System.Globalization;
using ProGPU.GameEngine.Assets;

namespace ProGPU.Samples.Suntrail.Game.Import;

public enum SmbxArtworkKind { Block, Background, Npc, SectionBackground }
public sealed record SmbxArtwork(string Path, RasterImage Image, SmbxNpcGraphics NpcGraphics)
{
    public bool IsBaseArtwork { get; init; }
}

/// <summary>
/// Preparation-only graphics resolver. Level folder precedes episode folder, then
/// the explicit base configuration;
/// caller-provided assets only, never ambient files or downloads. Indexing costs O(E)
/// for bounded package entries; each lookup probes a fixed number of names. Decoding
/// is cached once per asset, with at most 32 MiB retained pixels and at most 128 keys.
/// Use prepared results in render code; this class is not a render-time lazy cache.
/// </summary>
public sealed class SmbxArtworkCatalog
{
    public const int MaximumDecodedBytes = 32 * 1024 * 1024;
    private readonly AssetBundle _bundle;
    private readonly SmbxConfigPack? _definitions;
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(SmbxArtworkKind Kind, int Id), SmbxArtwork?> _prepared = [];
    private readonly Dictionary<(SmbxArtworkKind Kind, int Id), string> _failures = [];
    private readonly string _levelDirectory, _episodeDirectory;
    public int DecodedBytes { get; private set; }

    public SmbxArtworkCatalog(AssetBundle bundle, string levelPath, SmbxConfigPack? definitions = null)
    {
        ArgumentNullException.ThrowIfNull(bundle); _bundle = bundle; _definitions = definitions;
        levelPath = AssetBundle.ResolvePath("", levelPath);
        int slash = levelPath.LastIndexOf('/'), dot = levelPath.LastIndexOf('.');
        if (dot <= slash) throw new FormatException("The level asset path needs a file extension.");
        _episodeDirectory = slash < 0 ? "" : levelPath[..(slash + 1)];
        _levelDirectory = levelPath[..dot] + "/";
        foreach (string path in bundle.Paths)
            if (!_paths.TryAdd(path, path))
                throw new FormatException($"Case-ambiguous asset path '{path}'. Rename it for portable episode loading.");
    }

    public SmbxArtwork? Prepare(SmbxArtworkKind kind, int id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        string stem = Stem(kind) + id.ToString(CultureInfo.InvariantCulture);
        var key = (kind, id);
        if (_prepared.TryGetValue(key, out var previous)) return previous;
        if (_failures.TryGetValue(key, out var message)) throw new FormatException(message);
        if (_prepared.Count + _failures.Count >= AssetBundle.MaximumEntries)
            throw new FormatException("Artwork preparation supports at most 128 distinct object IDs.");
        try
        {
            string? path = FindImageForKind(_levelDirectory, stem, kind, id) ?? FindImageForKind(_episodeDirectory, stem, kind, id);
            bool baseArtwork = path is null;
            path ??= _definitions?.ResolveImage(kind, id);
            if (path is null) { _prepared.Add(key, null); return null; }
            string extension = System.IO.Path.GetExtension(path);
            if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"Base image '{path}' requires PNG, GIF or BMP in the current artwork adapter.");
            var bytes = baseArtwork ? _definitions!.ReadAsset(path) : _bundle.Get(path);
            var size = RasterImage.ReadDimensions(bytes.Span);
            int cost = checked(size.Width * size.Height * 4);
            if (cost > MaximumDecodedBytes - DecodedBytes)
                throw new FormatException("Prepared artwork exceeds the 32 MiB decoded-pixel budget.");
            SmbxNpcGraphics config = SmbxNpcGraphics.Empty;
            if (kind == SmbxArtworkKind.Npc)
            {
                string? configPath = Find(_levelDirectory + stem + ".txt") ?? Find(_episodeDirectory + stem + ".txt");
                if (configPath is not null) config = SmbxNpcGraphics.Read(_bundle.Get(configPath).Span);
            }
            var image = RasterImage.DecodeFirstFrame(bytes.Span);
            // Masks pair with an image in its chosen directory. Never accidentally
            // combine an episode mask with a different level-specific color image.
            if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                string maskName = path[..path.LastIndexOf('.')] + "m" + extension;
                string? maskPath = baseArtwork ? _definitions!.FindAsset(maskName) : Find(maskName);
                if (maskPath is not null)
                {
                    var maskBytes = baseArtwork ? _definitions!.ReadAsset(maskPath) : _bundle.Get(maskPath);
                    var maskSize = RasterImage.ReadDimensions(maskBytes.Span);
                    if (maskSize != size) throw new FormatException("Image and mask dimensions must match.");
                    image = image.WithBinaryMask(RasterImage.DecodeFirstFrame(maskBytes.Span), ignoreSourceAlpha: true, requireBlackOutsideMask: true);
                }
                else if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase))
                    for (int pixel = 3; pixel < image.ByteLength; pixel += 4)
                        if (image.Pixels[pixel] != 255)
                            throw new FormatException($"Transparent legacy GIF '{path}' needs its paired black/white mask; use PNG for native alpha artwork.");
            }
            var result = new SmbxArtwork(path, image, config) { IsBaseArtwork = baseArtwork };
            _prepared.Add(key, result); DecodedBytes += cost; return result;
        }
        catch (FormatException error) { _failures.Add(key, error.Message); throw; }
    }

    private string? Find(string path) => _paths.GetValueOrDefault(path);
    private string? FindImageForKind(string directory, string stem, SmbxArtworkKind kind, int id) => FindImage(directory, stem) ??
        (kind == SmbxArtworkKind.SectionBackground ? FindImage(directory, "background-2-" + id.ToString(CultureInfo.InvariantCulture)) : null);
    private string? FindImage(string directory, string stem)
    {
        // Explicit Suntrail import preference, not an assertion about all SMBX
        // engines: modern alpha PNG first, then legacy GIF and BMP.
        return Find(directory + stem + ".png") ?? Find(directory + stem + ".gif") ?? Find(directory + stem + ".bmp");
    }
    private static string Stem(SmbxArtworkKind kind) => kind switch
    {
        SmbxArtworkKind.Block => "block-", SmbxArtworkKind.Background => "background-",
        SmbxArtworkKind.Npc => "npc-", SmbxArtworkKind.SectionBackground => "background2-",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
