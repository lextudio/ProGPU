using System.Globalization;
using System.Text;

namespace ProGPU.Samples.Suntrail.Game.Import;

/// <summary>
/// Immutable source-preserving reader for sectioned object configuration. Supports
/// UTF-8/ASCII, key=value, quoted strings and unquoted inline semicolon comments.
/// Unknown values stay raw; no Qt variants, expressions, includes or scripts run.
/// O(B) time/storage; 256 KiB, 4096 lines/fields and 128 sections per file.
/// </summary>
public sealed class SmbxConfigDocument
{
    private readonly byte[] _source;
    private readonly Dictionary<string, SmbxConfigSection> _sections;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public IEnumerable<string> SectionNames => _sections.Keys;
    public int ByteLength => _source.Length;
    public int FieldCount { get; }
    private SmbxConfigDocument(byte[] source, Dictionary<string, SmbxConfigSection> sections, int fieldCount)
    { _source = source; _sections = sections; FieldCount = fieldCount; }
    public byte[] WriteOriginal() => (byte[])_source.Clone();
    public bool TryGetSection(string name, out SmbxConfigSection section) => _sections.TryGetValue(name, out section!);
    public SmbxConfigSection GetSection(string name) => _sections.TryGetValue(name, out var section) ? section : throw new FormatException($"Missing configuration section [{name}].");

    public static SmbxConfigDocument Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > 256 * 1024) throw new FormatException("Object configuration files support at most 256 KiB.");
        string text;
        try { text = Utf8.GetString(bytes); }
        catch (DecoderFallbackException e) { throw new FormatException("Object configuration requires UTF-8 or ASCII.", e); }
        var sections = new Dictionary<string, SmbxConfigSection>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? fields = null; int lineNumber = 0, fieldCount = 0;
        foreach (var raw in text.AsSpan().TrimStart('\uFEFF').EnumerateLines())
        {
            if (++lineNumber > 4096) throw new FormatException("Object configuration supports at most 4096 lines.");
            var line = raw.Trim(); if (line.IsEmpty || line[0] is ';' or '#') continue;
            line = RemoveComment(line).TrimEnd();
            if (line[0] == '[')
            {
                if (line[^1] != ']') throw new FormatException($"Invalid section on line {lineNumber}.");
                string name = line[1..^1].Trim().ToString(); ValidateName(name);
                if (sections.Count == 128) throw new FormatException("Too many configuration sections.");
                fields = new(StringComparer.OrdinalIgnoreCase);
                if (!sections.TryAdd(name, new(fields))) throw new FormatException($"Duplicate configuration section [{name}].");
                continue;
            }
            int separator = line.IndexOf('=');
            if (fields is null || separator <= 0) throw new FormatException($"Expected a sectioned key=value on line {lineNumber}.");
            string key = line[..separator].Trim().ToString(); ValidateName(key);
            if (++fieldCount > 4096 || !fields.TryAdd(key, line[(separator + 1)..].Trim().ToString()))
                throw new FormatException($"Duplicate or excessive configuration fields on line {lineNumber}.");
        }
        return new(bytes.ToArray(), sections, fieldCount);
    }
    private static void ValidateName(string name)
    {
        if (name.Length is 0 or > 128 || name.Any(c => char.IsControl(c) || c is '[' or ']' or '=' or ';' or '"'))
            throw new FormatException("Invalid configuration section or field name.");
    }
    private static ReadOnlySpan<char> RemoveComment(ReadOnlySpan<char> line)
    {
        bool quoted = false, escaped = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (escaped) { escaped = false; continue; }
            if (quoted && c == '\\') { escaped = true; continue; }
            if (c == '"') quoted = !quoted;
            else if (!quoted && c == ';') return line[..i];
        }
        if (quoted) throw new FormatException("Unclosed quoted configuration value.");
        return line;
    }
}

public sealed class SmbxConfigSection
{
    private readonly Dictionary<string, string> _fields;
    internal SmbxConfigSection(Dictionary<string, string> fields) => _fields = fields;
    public IEnumerable<string> Keys => _fields.Keys;
    public bool TryGetRaw(string key, out string raw) => _fields.TryGetValue(key, out raw!);
    public string? String(string key)
    {
        if (!_fields.TryGetValue(key, out string? raw)) return null;
        if (!raw.StartsWith('"')) return raw;
        if (raw.Length < 2 || raw[^1] != '"') throw new FormatException($"Invalid quoted value for {key}.");
        var value = new StringBuilder(raw.Length - 2);
        for (int i = 1; i < raw.Length - 1; i++)
        {
            char c = raw[i];
            if (c == '"') throw new FormatException($"Unescaped quote in {key}.");
            if (c == '\\')
            {
                if (++i == raw.Length - 1) throw new FormatException($"Truncated escape in {key}.");
                c = raw[i] switch { '\\' => '\\', '"' => '"', 'n' => '\n', 'r' => '\r', 't' => '\t',
                    _ => throw new FormatException($"Unsupported configuration string escape in {key}.") };
            }
            value.Append(c);
        }
        return value.ToString();
    }
    public int? Integer(string key)
    {
        string? value = String(key); if (value is null) return null;
        return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number) ? number :
            throw new FormatException($"Configuration {key} requires an integer.");
    }
    public double? Number(string key)
    {
        string? value = String(key); if (value is null) return null;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number) ? number :
            throw new FormatException($"Configuration {key} requires a finite invariant number.");
    }
    public bool? Boolean(string key)
    {
        string? value = String(key); if (value is null) return null;
        if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        throw new FormatException($"Configuration {key} requires 0/1 or false/true.");
    }
}
