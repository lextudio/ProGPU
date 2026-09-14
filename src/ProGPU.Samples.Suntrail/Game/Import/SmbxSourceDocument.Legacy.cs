using System.Globalization;
using System.Text;

namespace ProGPU.Samples.Suntrail.Game.Import;

public sealed partial class SmbxSourceDocument
{
    /// <summary>
    /// Original reader for the documented sequential SMBX format versions 0–64.
    /// All scalar tokens retain offsets and original spelling. O(B + F) parsing,
    /// O(B + F) owned storage. Legacy strings are literal, not LVLX-escaped or CSV.
    /// UTF-8/ASCII input; other legacy encodings and SMBX-38A remain separate work.
    /// </summary>
    public static SmbxSourceDocument ReadLegacy(ReadOnlySpan<byte> bytes, string fileName)
    {
        if (bytes.Length > MaximumBytes) throw new FormatException("SMBX source files must be at most 8 MiB.");
        string text;
        try { text = Utf8.GetString(bytes); }
        catch (DecoderFallbackException e) { throw new FormatException("This legacy reader currently accepts UTF-8 or ASCII source.", e); }
        var reader = new LegacyReader(text);
        var records = new List<SmbxSourceRecord>();
        var insertions = new Dictionary<string, int>(StringComparer.Ordinal);
        int fieldsRead = 0;
        List<SmbxSourceField> Begin() => new();
        void Finish(string section, int index, List<SmbxSourceField> fields)
        {
            if (records.Count == MaximumRecords || (fieldsRead += fields.Count) > MaximumFields)
                throw reader.Error("Too many records or fields.");
            var first = fields[0]; var last = fields[^1];
            records.Add(new(section, section, reader.RowLine, first.Offset, last.Offset + last.RawValue.Length, fields.ToArray(), index));
        }
        SmbxSourceField Take(List<SmbxSourceField> fields, string name, LegacyType type = LegacyType.Number)
        {
            var token = reader.Read();
            if (fields.Count == 0) reader.RowLine = token.Line;
            var field = new SmbxSourceField(name, token.Raw, token.Offset, true);
            try
            {
                switch (type)
                {
                    case LegacyType.Number: field.GetNumber(); break;
                    case LegacyType.Integer: field.GetInteger(); break;
                    case LegacyType.String: field.GetString(); break;
                    case LegacyType.Boolean: field.GetBoolean(); break;
                }
            }
            catch (FormatException e) { throw reader.Error($"{name}: {e.Message}"); }
            fields.Add(field); return field;
        }
        void Numbers(List<SmbxSourceField> fields, params string[] names) { foreach (string name in names) Take(fields, name); }
        void Strings(List<SmbxSourceField> fields, params string[] names) { foreach (string name in names) Take(fields, name, LegacyType.String); }
        void Bools(List<SmbxSourceField> fields, params string[] names) { foreach (string name in names) Take(fields, name, LegacyType.Boolean); }

        var header = Begin();
        int version = Take(header, "VERSION", LegacyType.Integer).GetInteger();
        if (version is < 0 or > 64) throw reader.Error("Expected an SMBX sequential format version from 0 to 64; SMBX-38A uses another grammar.");
        if (version >= 17) Take(header, "SZ", LegacyType.Integer);
        if (version >= 60) Strings(header, "TL");
        Finish("HEAD", 0, header);
        for (int section = 0; section < (version <= 7 ? 6 : 21); section++)
        {
            var fields = Begin(); Numbers(fields, "L", "T", "B", "R");
            Take(fields, "MZ", LegacyType.Integer); Take(fields, "LEGACY_BG_COLOR", LegacyType.Integer);
            Bools(fields, "CS", "OE"); Take(fields, "BG", LegacyType.Integer);
            if (version >= 1) Bools(fields, "SR");
            if (version >= 30) Bools(fields, "UW");
            if (version >= 2) Strings(fields, "MF");
            Finish("SECTION", section, fields);
        }
        for (int player = 0; player < 2; player++)
        { var fields = Begin(); Numbers(fields, "X", "Y", "W", "H"); Finish("STARTPOINT", player, fields); }

        int ordinal = 0;
        while (!reader.Next())
        {
            var fields = Begin(); Numbers(fields, "X", "Y", "H", "W");
            Take(fields, "ID", LegacyType.Integer); Take(fields, "LEGACY_CONTENT", LegacyType.Integer); Bools(fields, "IV");
            if (version >= 61) Bools(fields, "SL");
            if (version >= 10) Strings(fields, "LR");
            if (version >= 14) Strings(fields, "ED", "EH", "EE");
            Finish("BLOCK", ordinal++, fields);
        }
        insertions.Add("BLOCK", reader.LastSeparatorOffset);
        ordinal = 0;
        while (!reader.Next())
        {
            var fields = Begin(); Numbers(fields, "X", "Y"); Take(fields, "ID", LegacyType.Integer);
            if (version >= 10) Strings(fields, "LR");
            Finish("BGO", ordinal++, fields);
        }
        insertions.Add("BGO", reader.LastSeparatorOffset);
        ordinal = 0;
        while (!reader.Next())
        {
            var fields = Begin(); Numbers(fields, "X", "Y"); Take(fields, "D", LegacyType.Integer);
            int id = Take(fields, "ID", LegacyType.Integer).GetInteger();
            // Conditional wire fields from the specification's NPC table. Content
            // remains raw; legacy block/NPC encodings are not normalized or lost.
            bool special = LegacyNpcHasSpecial(id, version);
            int firstSpecial = special ? Take(fields, "S1", LegacyType.Integer).GetInteger() : 0;
            if (id == 91 && firstSpecial == 288) Take(fields, "S2", LegacyType.Integer);
            if (version >= 3 && Take(fields, "GE", LegacyType.Boolean).GetBoolean())
            { Take(fields, "GD", LegacyType.Integer); Take(fields, "GT", LegacyType.Integer); Take(fields, "GM", LegacyType.Integer); }
            if (version >= 5) Strings(fields, "MG");
            if (version >= 6) Bools(fields, "FD", "NM");
            if (version >= 9) Bools(fields, "BS");
            if (version >= 10) Strings(fields, "LR", "EA", "ED", "ET");
            if (version >= 14) Strings(fields, "EE");
            if (version >= 63) Strings(fields, "LA");
            Finish("NPC", ordinal++, fields);
        }
        insertions.Add("NPC", reader.LastSeparatorOffset);
        ordinal = 0;
        while (version >= 10 ? !reader.Next() : !reader.End)
        {
            var fields = Begin(); Numbers(fields, "IX", "IY", "OX", "OY");
            Take(fields, "ID", LegacyType.Integer); Take(fields, "OD", LegacyType.Integer); Take(fields, "DT", LegacyType.Integer);
            if (version >= 3) { Strings(fields, "LF"); Take(fields, "LI", LegacyType.Integer); Bools(fields, "ET"); }
            if (version >= 4) { Bools(fields, "EX"); Numbers(fields, "WX", "WY"); }
            if (version >= 7) Take(fields, "SL", LegacyType.Integer);
            if (version >= 12) { Strings(fields, "LR"); Bools(fields, "LEGACY_HIDDEN"); }
            if (version >= 23) Bools(fields, "NV");
            if (version >= 25) Bools(fields, "AI");
            if (version >= 26) Bools(fields, "LC");
            Finish("DOORS", ordinal++, fields);
        }
        insertions.Add("DOORS", version >= 10 ? reader.LastSeparatorOffset : text.Length);
        if (version >= 10)
        {
            ordinal = 0;
            while (!reader.Next())
            {
                if (version < 29) throw reader.Error("This format version has no physical environment records.");
                var fields = Begin(); Numbers(fields, "X", "Y", "W", "H", "LEGACY_BUOY");
                if (version >= 62) Bools(fields, "LEGACY_QUICKSAND");
                Strings(fields, "LR"); Finish("PHYSICS", ordinal++, fields);
            }
            if (version >= 29) insertions.Add("PHYSICS", reader.LastSeparatorOffset);
            ordinal = 0;
            while (!reader.Next())
            { var fields = Begin(); Strings(fields, "LR"); Bools(fields, "HD"); Finish("LAYERS", ordinal++, fields); }
            insertions.Add("LAYERS", reader.LastSeparatorOffset);
            ordinal = 0;
            while (!reader.End)
            {
                var fields = Begin(); Strings(fields, "ET");
                if (version >= 11) Strings(fields, "MG");
                if (version >= 14) Take(fields, "SD", LegacyType.Integer);
                if (version >= 18) Take(fields, "EG", LegacyType.Integer);
                // Twenty slots plus the documented mandatory empty terminal slots.
                for (int item = 0; item <= 20; item++)
                {
                    Strings(fields, $"LH{item}", $"LS{item}");
                    if (version >= 14) Strings(fields, $"LT{item}");
                }
                if (version >= 13)
                    for (int section = 0; section < 21; section++)
                    { Numbers(fields, $"SM{section}", $"SB{section}", $"SL{section}", $"ST{section}", $"SD{section}", $"SR{section}"); }
                if (version >= 26) { Strings(fields, "TE"); Take(fields, "TD", LegacyType.Integer); }
                if (version >= 27) Bools(fields, "DS");
                if (version >= 28) Bools(fields, "PC_ALT_JUMP", "PC_ALT_RUN", "PC_DOWN", "PC_DROP", "PC_JUMP", "PC_LEFT", "PC_RIGHT", "PC_RUN", "PC_START", "PC_UP");
                if (version >= 32) { Bools(fields, "AU"); Strings(fields, "ML"); Numbers(fields, "MX", "MY"); }
                if (version >= 33) { Numbers(fields, "AX", "AY"); Take(fields, "AS", LegacyType.Integer); }
                Finish("EVENTS_CLASSIC", ordinal++, fields);
            }
        }
        return new(bytes.ToArray(), text, fileName, records.ToArray(), true, version, insertions);
    }

    public static string EncodeLegacyString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > MaximumBytes || value.Contains('"')) throw new FormatException("Legacy LVL strings cannot safely represent embedded quotes.");
        foreach (char c in value) if (char.IsControl(c) && c is not ('\r' or '\n' or '\t')) throw new FormatException("Invalid legacy string control character.");
        return "\"" + value + "\"";
    }
    internal static string DecodeLegacyString(string raw)
    {
        if (raw is null || raw.Length < 2 || raw[0] != '"' || raw[^1] != '"' || raw.AsSpan(1, raw.Length - 2).IndexOf('"') >= 0)
            throw new FormatException("Expected a literal quoted legacy LVL string.");
        return raw[1..^1];
    }
    private static void ValidateLegacyValue(string raw)
    {
        var reader = new LegacyReader(raw); var token = reader.Read();
        if (!reader.End) throw reader.Error("An edit must contain exactly one legacy field value.");
        if (token.Raw.StartsWith('"')) DecodeLegacyString(token.Raw);
        else if (token.Raw is not ("#TRUE#" or "#FALSE#")) new SmbxSourceField("value", token.Raw, 0, true).GetNumber();
    }

    private enum LegacyType { Number, Integer, String, Boolean }
    private static bool LegacyNpcHasSpecial(int id, int version) =>
        id is 91 or 96 or 283 or 284 or 121 or 122 or 123 or 124 or 161 or 176 or 177 or
            243 or 244 or 229 or 230 or 232 or 233 or 234 or 236 or 260 or 288 or 289 ||
        (id == 76 && version >= 15) || (id == 28 && version >= 30);
    private readonly record struct LegacyToken(string Raw, int Offset, int Line);
    private sealed class LegacyReader(string text)
    {
        private int _position = text.Length > 0 && text[0] == '\ufeff' ? 1 : 0;
        private int _line = 1;
        public int RowLine { get; set; }
        public int LastSeparatorOffset { get; private set; }
        public bool End { get { SkipWhitespace(); return _position == text.Length; } }
        public FormatException Error(string message) => new($"SMBX LVL line {_line}: {message}");
        private void Advance()
        {
            char c = text[_position++];
            if (c == '\n' || c == '\r' && (_position == text.Length || text[_position] != '\n')) _line++;
        }
        private void SkipWhitespace()
        { while (_position < text.Length && text[_position] is ' ' or '\t' or '\r' or '\n') Advance(); }
        public bool Next()
        {
            SkipWhitespace();
            if (_position == text.Length) throw Error("Missing next separator.");
            if (!text.AsSpan(_position).StartsWith("\"next\"")) return false;
            var token = Read();
            if (token.Raw != "\"next\"") throw Error("Malformed next separator.");
            LastSeparatorOffset = token.Offset;
            return true;
        }
        public LegacyToken Read()
        {
            SkipWhitespace();
            if (_position == text.Length) throw Error("Unexpected end of file.");
            int start = _position, line = _line;
            if (text[_position] == '"')
            {
                Advance();
                while (_position < text.Length && text[_position] != '"')
                {
                    char c = text[_position];
                    if (char.IsControl(c) && c is not ('\r' or '\n' or '\t')) throw Error("Control character in string.");
                    Advance();
                }
                if (_position == text.Length) throw Error("Unterminated string.");
                Advance();
                int end = _position;
                while (_position < text.Length && text[_position] is ' ' or '\t') Advance();
                if (_position < text.Length && text[_position] is not ('\r' or '\n')) throw Error("Only one value is allowed per legacy field line; embedded quotes are not escaped by doubling.");
                return new(text[start..end], start, line);
            }
            while (_position < text.Length && text[_position] is not ('\r' or '\n'))
            {
                if (char.IsControl(text[_position]) && text[_position] != '\t') throw Error("Control character in scalar value.");
                Advance();
            }
            int valueEnd = _position;
            while (valueEnd > start && text[valueEnd - 1] is ' ' or '\t') valueEnd--;
            return new(text[start..valueEnd], start, line);
        }
    }
}
