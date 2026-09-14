using System.Globalization;
using System.Text;
using ProGPU.GameEngine.Assets;

namespace ProGPU.Samples.Suntrail.Game.Import;

/// <summary>
/// Load-time NPC.txt values. Unknown options remain inspectable; none execute code.
/// O(B) parsing/storage for at most 64 KiB and 1024 lines. Missing values are not
/// fabricated from an NPC ID: the caller needs an explicit base configuration.
/// </summary>
public sealed class SmbxNpcGraphics
{
    private readonly Dictionary<string, string> _values;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public IEnumerable<string> Keys => _values.Keys;
    private SmbxNpcGraphics(Dictionary<string, string> values) => _values = values;
    public static SmbxNpcGraphics Empty { get; } = new(new(StringComparer.OrdinalIgnoreCase));
    public bool TryGet(string key, out string value) => _values.TryGetValue(key, out value!);

    public static SmbxNpcGraphics Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 65_536) throw new FormatException("NPC configuration must be at most 64 KiB.");
        string text;
        try { text = Utf8.GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException e) { throw new FormatException("NPC configuration requires UTF-8 or ASCII.", e); }
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int count = 0;
        foreach (var source in text.AsSpan().EnumerateLines())
        {
            if (++count > 1024) throw new FormatException("NPC configuration supports at most 1024 lines.");
            var line = source.Trim();
            if (line.IsEmpty || line[0] is '#' or ';') continue;
            int equals = line.IndexOf('=');
            if (equals <= 0) throw new FormatException($"NPC configuration line {count} needs key=value.");
            string key = line[..equals].Trim().ToString();
            if (key.Length is 0 or > 64 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')))
                throw new FormatException($"Invalid NPC option on line {count}.");
            if (!values.TryAdd(key, line[(equals + 1)..].Trim().ToString()))
                throw new FormatException($"Duplicate NPC option '{key}'.");
        }
        return new(values);
    }

    public int? Integer(string key)
    {
        if (!_values.TryGetValue(key, out string? value)) return null;
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number))
            throw new FormatException($"NPC option '{key}' requires an integer.");
        return number;
    }

    /// <summary>
    /// Resolves a simple vertical frame sheet only when all layout values are known.
    /// Does not infer NPC animation algorithms, timing, collision, or state transitions.
    /// </summary>
    public SmbxNpcSheet? ResolveSheet(RasterImage image, SmbxNpcGraphics? defaults = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        int? Value(string key) => Integer(key) ?? defaults?.Integer(key);
        int? width = Value("gfxwidth"), height = Value("gfxheight"), frames = Value("frames"), style = Value("framestyle");
        if (width is null || height is null || frames is null || style is null) return null;
        if (width <= 0 || height <= 0 || frames is <= 0 or > 4096 || style is < 0 or > 2)
            throw new FormatException("Simple NPC sheets require positive frame dimensions/count and framestyle 0, 1 or 2.");
        int groups = style == 0 ? 1 : style == 1 ? 2 : 4;
        if (width != image.Width || (long)height * frames * groups != image.Height)
            throw new FormatException("NPC sheet dimensions do not match its explicit frame layout.");
        return new(width.Value, height.Value, frames.Value, style.Value,
            Value("gfxoffsetx") ?? 0, Value("gfxoffsety") ?? 0);
    }
}

/// <summary>Original bounded editor frame selection, independent of any NPC runtime algorithm.</summary>
public sealed class SmbxNpcSheet
{
    public int Width { get; }
    public int Height { get; }
    public int Frames { get; }
    public int Style { get; }
    public int OffsetX { get; }
    public int OffsetY { get; }
    internal SmbxNpcSheet(int width, int height, int frames, int style, int offsetX, int offsetY)
    { Width = width; Height = height; Frames = frames; Style = style; OffsetX = offsetX; OffsetY = offsetY; }

    public (int X, int Y, int Width, int Height) Frame(int frame, bool right, bool held = false)
    {
        if ((uint)frame >= (uint)Frames) throw new ArgumentOutOfRangeException(nameof(frame));
        if (held && Style != 2) throw new ArgumentException("This sheet has no held-state frame group.", nameof(held));
        int group = Style == 0 ? 0 : (right ? 1 : 0) + (held ? 2 : 0);
        return (0, (group * Frames + frame) * Height, Width, Height);
    }
}
