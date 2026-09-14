using System.Globalization;
using System.Text;

namespace ProGPU.Samples.Suntrail.Game.Import;

/// <summary>
/// Original, bounded LVLX syntax reader from the maintainer's format specification.
/// Unknown sections/fields and original whitespace remain intact. This is a source
/// document, not a declaration that the game implements every described behavior.
/// Parse is O(B * D + F) in input characters, array depth (at most 16) and fields;
/// source/field storage is O(B + F). There is no parsing during gameplay.
/// </summary>
public sealed partial class SmbxSourceDocument
{
    public const int MaximumBytes = 8 * 1024 * 1024;
    public const int MaximumRecords = 65_536;
    public const int MaximumFields = 262_144;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly byte[] _original;
    private readonly SmbxSourceRecord[] _records;
    private readonly string _text;
    private readonly Dictionary<string, int> _insertions;
    public string FileName { get; }
    public bool IsLegacy { get; }
    public int FormatVersion { get; }
    public ReadOnlySpan<SmbxSourceRecord> Records => _records;

    private SmbxSourceDocument(byte[] original, string text, string fileName, SmbxSourceRecord[] records, bool legacy = false, int version = 0, Dictionary<string, int>? insertions = null)
    { _original = original; _text = text; FileName = fileName; _records = records; IsLegacy = legacy; FormatVersion = version; _insertions = insertions ?? new(StringComparer.Ordinal); }

    public static SmbxSourceDocument ReadLvlx(ReadOnlySpan<byte> bytes, string fileName)
    {
        if (bytes.Length > MaximumBytes) throw new FormatException("SMBX source files must be at most 8 MiB.");
        string text;
        try { text = Utf8.GetString(bytes); }
        catch (DecoderFallbackException e) { throw new FormatException("LVLX source must contain valid UTF-8.", e); }
        var records = new List<SmbxSourceRecord>();
        var sections = new List<string>();
        var insertions = new Dictionary<string, int>(StringComparer.Ordinal);
        string path = "";
        int position = text.Length > 0 && text[0] == '\ufeff' ? 1 : 0;
        int line = 0, fieldCount = 0, sectionCount = 0;
        bool started = false;
        while (position < text.Length)
        {
            int start = position;
            int lineStart = start;
            while (position < text.Length && text[position] is not ('\r' or '\n')) position++;
            int end = position;
            if (position < text.Length && text[position++] == '\r' && position < text.Length && text[position] == '\n') position++;
            line++;
            while (start < end && text[start] is ' ' or '\t') start++;
            while (end > start && text[end - 1] is ' ' or '\t') end--;
            if (start == end) continue;
            var content = text.AsSpan(start, end - start);
            if (!started)
            {
                if (!content.SequenceEqual("HEAD")) throw Error(line, "An LVLX file must begin with HEAD.");
                started = true;
            }
            if (IsMarker(content))
            {
                string marker = content.ToString();
                if (marker.EndsWith("_END", StringComparison.Ordinal))
                {
                    if (sections.Count == 0 || sections[^1] != marker[..^4]) throw Error(line, "Mismatched section terminator.");
                    if (sections.Count == 1) insertions.TryAdd(sections[0], lineStart);
                    sections.RemoveAt(sections.Count - 1);
                }
                else
                {
                    if (sections.Count == 16 || ++sectionCount > 4096) throw Error(line, "Too many or too deeply nested data sections.");
                    sections.Add(marker);
                }
                path = string.Join('/', sections);
                continue;
            }
            if (sections.Count == 0) throw Error(line, "Data rows must belong to a section.");
            if (records.Count == MaximumRecords) throw Error(line, "Too many data rows.");
            var fields = ParseFields(text, start, end, line);
            fieldCount += fields.Length;
            if (fieldCount > MaximumFields) throw Error(line, "Too many data fields.");
            records.Add(new(sections[^1], path, line, start, end, fields));
        }
        if (!started || sections.Count != 0) throw Error(line, "Missing or unclosed LVLX section.");
        return new(bytes.ToArray(), text, fileName, records.ToArray(), insertions: insertions);
    }

    /// <summary>Returns an owned exact copy, including encoding BOM and line endings.</summary>
    public byte[] WriteOriginal() => (byte[])_original.Clone();

    /// <summary>
    /// Transactionally replace existing field values. Unedited bytes retain their
    /// spelling and order, including unknown data. O(B + E log E), bounded to 4096
    /// edits; use at save/apply time, never once per pointer movement. The new source
    /// is parsed before it becomes visible. No scripts or file references execute.
    /// </summary>
    public SmbxSourceDocument WithFields(ReadOnlySpan<SmbxFieldEdit> edits)
    {
        if (edits.Length > 4096) throw new FormatException("At most 4096 source fields can be changed in one edit.");
        if (edits.IsEmpty) return this;
        var replacements = new Replacement[edits.Length];
        for (int i = 0; i < edits.Length; i++)
        {
            var edit = edits[i];
            if ((uint)edit.Record >= _records.Length) throw new ArgumentOutOfRangeException(nameof(edits));
            var field = _records[edit.Record].Get(edit.Field);
            string raw = edit.RawValue ?? throw new ArgumentException("An edited value cannot be null.", nameof(edits));
            if (IsLegacy) ValidateLegacyValue(raw);
            else ValidateValue(raw.AsSpan(), _records[edit.Record].Line);
            replacements[i] = new(field.Offset, field.RawValue.Length, raw);
        }
        Array.Sort(replacements, static (a, b) => a.Offset.CompareTo(b.Offset));
        long length = _text.Length;
        int lastEnd = -1;
        foreach (var replacement in replacements)
        {
            if (replacement.Offset < lastEnd) throw new FormatException("Source edits must not overlap or repeat a field.");
            lastEnd = replacement.Offset + replacement.Length;
            length += replacement.Value.Length - replacement.Length;
        }
        if (length > MaximumBytes) throw new FormatException("The edited source is too large.");
        var output = new StringBuilder((int)length);
        int copied = 0;
        foreach (var replacement in replacements)
        {
            if (replacement.Offset < copied) throw new FormatException("Source edits must not overlap or repeat a field.");
            output.Append(_text, copied, replacement.Offset - copied); output.Append(replacement.Value);
            copied = replacement.Offset + replacement.Length;
        }
        output.Append(_text, copied, _text.Length - copied);
        string result = output.ToString();
        if (Utf8.GetByteCount(result) > MaximumBytes) throw new FormatException("The edited source is too large.");
        return IsLegacy ? ReadLegacy(Utf8.GetBytes(result), FileName) : ReadLvlx(Utf8.GetBytes(result), FileName);
    }

    public static string EncodeString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > MaximumBytes) throw new FormatException("The string is too large.");
        var encoded = new StringBuilder(value.Length + 2); encoded.Append('"');
        foreach (char c in value)
        {
            if (c == '\n') encoded.Append("\\n");
            else if (c is '"' or '\\' or ';' or ':' or '[' or ']' or ',' or '%') { encoded.Append('\\'); encoded.Append(c); }
            else if (char.IsControl(c)) throw new FormatException("LVLX strings cannot contain this control character.");
            else encoded.Append(c);
        }
        encoded.Append('"'); return encoded.ToString();
    }

    internal static string DecodeString(string raw)
    {
        if (raw is null) throw new FormatException("The field requires a string.");
        ValidateValue(raw.AsSpan(), 0);
        if (raw.StartsWith('H'))
        {
            try { return Utf8.GetString(Convert.FromHexString(raw.AsSpan(1))); }
            catch (Exception e) when (e is FormatException or DecoderFallbackException)
            { throw new FormatException("Malformed LVLX hex string.", e); }
        }
        if (raw.Length < 2 || raw[0] != '"' || raw[^1] != '"') throw new FormatException("The field requires a string.");
        var value = new StringBuilder(raw.Length - 2);
        for (int i = 1; i < raw.Length - 1; i++)
        {
            char c = raw[i];
            if (c == '\\') { c = raw[++i]; value.Append(c == 'n' ? '\n' : c); }
            else value.Append(c);
        }
        return value.ToString();
    }

    private static SmbxSourceField[] ParseFields(string text, int start, int end, int line)
    {
        var fields = new List<SmbxSourceField>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        int cursor = start;
        while (cursor < end)
        {
            while (cursor < end && text[cursor] is ' ' or '\t') cursor++;
            if (cursor == end) break;
            int nameStart = cursor;
            while (cursor < end && (char.IsAsciiLetterOrDigit(text[cursor]) || text[cursor] is '_' or '-')) cursor++;
            if (cursor == nameStart || !char.IsAsciiLetter(text[nameStart])) throw Error(line, "Expected a field marker.");
            string name = text[nameStart..cursor];
            while (cursor < end && text[cursor] is ' ' or '\t') cursor++;
            if (cursor == end || text[cursor++] != ':') throw Error(line, "Expected ':' after a field marker.");
            while (cursor < end && text[cursor] is ' ' or '\t') cursor++;
            int valueStart = cursor;
            bool quoted = false, escaped = false; int depth = 0;
            while (cursor < end)
            {
                char c = text[cursor];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                }
                else if (c == '"') quoted = true;
                else if (c == '[') { if (++depth > 16) throw Error(line, "Arrays are nested too deeply."); }
                else if (c == ']') { if (--depth < 0) throw Error(line, "Unmatched array terminator."); }
                else if (c == ';' && depth == 0) break;
                cursor++;
            }
            int valueEnd = cursor;
            while (valueEnd > valueStart && text[valueEnd - 1] is ' ' or '\t') valueEnd--;
            if (quoted || depth != 0) throw Error(line, "Unterminated field value.");
            var value = text.AsSpan(valueStart, valueEnd - valueStart);
            ValidateValue(value, line);
            if (!names.Add(name)) throw Error(line, $"Field '{name}' is repeated in one row.");
            if (fields.Count == 256) throw Error(line, "A data row supports at most 256 fields.");
            fields.Add(new(name, value.ToString(), valueStart));
            if (cursor < end) cursor++;
        }
        return fields.ToArray();
    }

    private static void ValidateValue(ReadOnlySpan<char> value, int line, int depth = 0)
    {
        if (value.IsEmpty) throw Error(line, "A field value cannot be empty; use an empty quoted string.");
        if (depth > 16) throw Error(line, "Arrays are nested too deeply.");
        if (value[0] == '"')
        {
            if (value.Length < 2 || value[^1] != '"') throw Error(line, "Unterminated string.");
            for (int i = 1; i < value.Length - 1; i++)
            {
                char c = value[i];
                if (c == '\\')
                {
                    if (++i == value.Length - 1 || "n\"\\;:[],%".AsSpan().IndexOf(value[i]) < 0) throw Error(line, "Unsupported string escape.");
                }
                else if (c == '"' || char.IsControl(c)) throw Error(line, "Unescaped quote or control character in string.");
            }
            return;
        }
        if (value[0] == '[')
        {
            if (value[^1] != ']') throw Error(line, "Unterminated array.");
            var body = value[1..^1];
            if (body.Trim().IsEmpty) return;
            int start = 0, nested = 0; bool quoted = false, escaped = false;
            for (int i = 0; i <= body.Length; i++)
            {
                char c = i == body.Length ? ',' : body[i];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                }
                else if (c == '"') quoted = true;
                else if (c == '[') nested++;
                else if (c == ']') nested--;
                else if (c == ',' && nested == 0)
                { ValidateValue(body[start..i].Trim(), line, depth + 1); start = i + 1; }
            }
            if (quoted || nested != 0 || start != body.Length + 1) throw Error(line, "Malformed array.");
            return;
        }
        if (value[0] is 'H' or 'B')
        {
            if ((value.Length - 1) % 2 != 0 || !ContainsOnlyHex(value[1..])) throw Error(line, "Malformed hex value.");
            return;
        }
        // Boolean arrays are unbounded by numeric precision; their type is decided
        // by the field contract, not by parsing them as a binary integer.
        bool bits = true;
        foreach (char c in value)
        {
            if (char.IsControl(c)) throw Error(line, "Control character in scalar value.");
            if (c is not ('0' or '1')) bits = false;
        }
        if (bits) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number))
            throw Error(line, "Expected a finite number, string, hex value or array.");
    }

    private static bool ContainsOnlyHex(ReadOnlySpan<char> text)
    { foreach (char c in text) if (!char.IsAsciiHexDigit(c)) return false; return true; }
    private static bool IsMarker(ReadOnlySpan<char> text)
    { if (text.IsEmpty || text[0] is not (>= 'A' and <= 'Z')) return false; foreach (char c in text) if (!(c is >= 'A' and <= 'Z') && c != '_' && !char.IsAsciiDigit(c)) return false; return true; }
    private static FormatException Error(int line, string message) => new($"LVLX line {line}: {message}");
    private readonly record struct Replacement(int Offset, int Length, string Value);
}

public readonly record struct SmbxFieldEdit(int Record, string Field, string RawValue);

public sealed class SmbxSourceRecord
{
    private readonly SmbxSourceField[] _fields;
    public string Section { get; }
    public string SectionPath { get; }
    public int Line { get; }
    public int IndexInSection { get; }
    internal int Start { get; }
    internal int End { get; }
    public ReadOnlySpan<SmbxSourceField> Fields => _fields;
    internal SmbxSourceRecord(string section, string path, int line, int start, int end, SmbxSourceField[] fields, int index = -1)
    { Section = section; SectionPath = path; Line = line; Start = start; End = end; _fields = fields; IndexInSection = index; }
    public bool TryGet(string name, out SmbxSourceField field)
    { foreach (var candidate in _fields) if (candidate.Name == name) { field = candidate; return true; } field = default; return false; }
    public SmbxSourceField Get(string name) => TryGet(name, out var field) ? field : throw new FormatException($"{Section} line {Line} is missing '{name}'.");
}

public readonly record struct SmbxSourceField(string Name, string RawValue, int Offset, bool IsLegacy = false)
{
    public string GetString() => IsLegacy ? SmbxSourceDocument.DecodeLegacyString(RawValue) : SmbxSourceDocument.DecodeString(RawValue);
    public double GetNumber() => double.TryParse(RawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value)
        ? value : throw new FormatException($"Field '{Name}' requires a finite number.");
    public int GetInteger()
    {
        // Identifier fields use the specification's integer syntax. Do not round
        // fractional values or exponent underflow through floating-point parsing.
        if (!int.TryParse(RawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            throw new FormatException($"Field '{Name}' requires a 32-bit integer.");
        return value;
    }
    public bool GetBoolean() => (IsLegacy, RawValue) switch
    { (false, "0") or (true, "#FALSE#") => false, (false, "1") or (true, "#TRUE#") => true,
        _ => throw new FormatException($"Field '{Name}' requires a {(IsLegacy ? "#TRUE# or #FALSE#" : "0 or 1")} boolean.") };
}
