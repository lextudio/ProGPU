using System.Globalization;
using System.Text;

namespace ProGPU.Samples.Suntrail.Game.Import;

/// <summary>Exact source-text delta; expected text guards undo against a stale document.</summary>
internal readonly record struct SmbxSourceSplice(int Offset, string Before, string After)
{
    public SmbxSourceSplice Reverse() => new(Offset, After, Before);
    public int HistoryBytes => checked((Before.Length + After.Length) * sizeof(char));
}

public sealed partial class SmbxSourceDocument
{
    internal SmbxSourceSplice PatchObjectFields(int record, ReadOnlySpan<SmbxFieldEdit> changes)
    {
        if (IsLegacy) throw new FormatException("Missing fields cannot be inserted into a sequential legacy record.");
        var row = _records[record];
        if (row.Fields.IsEmpty) throw new FormatException("Object record has no source fields.");
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var change in changes)
        {
            if (change.Record != record || change.Field.Length == 0 || !char.IsAsciiLetter(change.Field[0]) || change.Field.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')))
                throw new FormatException("Invalid object property transaction.");
            ValidateValue(change.RawValue.AsSpan(), row.Line);
            if (!replacements.TryAdd(change.Field, change.RawValue)) throw new FormatException("A property occurs twice in the transaction.");
        }
        var output = new StringBuilder(); int copied = row.Start;
        foreach (var field in row.Fields)
        {
            if (!replacements.Remove(field.Name, out string? value)) continue;
            output.Append(_text, copied, field.Offset - copied).Append(value); copied = field.Offset + field.RawValue.Length;
        }
        int endValue = row.Fields[^1].Offset + row.Fields[^1].RawValue.Length;
        output.Append(_text, copied, endValue - copied);
        foreach (var (name, value) in replacements) output.Append(';').Append(name).Append(':').Append(value);
        output.Append(_text, endValue, row.End - endValue);
        return new(row.Start, _text[row.Start..row.End], output.ToString());
    }

    internal SmbxSourceSplice InsertObjectField(int record, string field, string value)
    {
        if (IsLegacy) throw new FormatException("This legacy version has no slot for the selected property.");
        if ((uint)record >= _records.Length) throw new ArgumentOutOfRangeException(nameof(record));
        var row = _records[record];
        if (field is not ("LR" or "D") || row.Fields.IsEmpty || row.TryGet(field, out _))
            throw new FormatException("This property cannot be inserted into the source record.");
        ValidateValue(value.AsSpan(), row.Line);
        var last = row.Fields[^1];
        // Insert after the final value, keeping its original trailing separator,
        // whitespace and line ending untouched. Reparse validates the transaction.
        return new(last.Offset + last.RawValue.Length, "", ";" + field + ":" + value);
    }

    public static SmbxSourceDocument CreateLvlx(string title = "New level")
    {
        // Original blank authoring canvas, not a copied game layout. A player
        // anchor and section are fixed scaffolding; the palette supplies objects.
        string text = "HEAD\r\nTL:" + EncodeString(title) + ";\r\nHEAD_END\r\n" +
            "SECTION\r\nSC:0;L:0;T:0;R:1600;B:900;\r\nSECTION_END\r\n" +
            "STARTPOINT\r\nID:1;X:64;Y:700;D:1;\r\nSTARTPOINT_END\r\n";
        return ReadLvlx(Utf8.GetBytes(text), "new-level.lvlx");
    }

    internal SmbxSourceDocument WithSplice(SmbxSourceSplice splice)
    {
        if (splice.Offset < 0 || splice.Offset > _text.Length || splice.Before.Length > _text.Length - splice.Offset ||
            !_text.AsSpan(splice.Offset, splice.Before.Length).SequenceEqual(splice.Before))
            throw new FormatException("The source changed since this edit was recorded.");
        long length = (long)_text.Length - splice.Before.Length + splice.After.Length;
        if (length > MaximumBytes) throw new FormatException("The edited source is too large.");
        var text = new StringBuilder((int)length).Append(_text, 0, splice.Offset).Append(splice.After)
            .Append(_text, splice.Offset + splice.Before.Length, _text.Length - splice.Offset - splice.Before.Length).ToString();
        if (Utf8.GetByteCount(text) > MaximumBytes) throw new FormatException("The edited source is too large.");
        var bytes = Utf8.GetBytes(text);
        return IsLegacy ? ReadLegacy(bytes, FileName) : ReadLvlx(bytes, FileName);
    }

    internal SmbxSourceSplice DeleteGeometryRecord(int record)
    {
        if ((uint)record >= _records.Length) throw new ArgumentOutOfRangeException(nameof(record));
        var row = _records[record];
        if (row.SectionPath != row.Section || row.Section is not ("BLOCK" or "BGO" or "NPC" or "DOORS" or "PHYSICS"))
            throw new FormatException("Delete supports objects and warps. Section frames and player slots remain part of the level.");
        // Keep surrounding whitespace, line endings and section/next delimiters.
        return new(row.Start, _text[row.Start..row.End], "");
    }

    internal SmbxSourceSplice InsertGeometryRecord(SmbxGeometryKind kind, int id, SmbxBounds bounds)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        string section = kind switch
        {
            SmbxGeometryKind.Block => "BLOCK", SmbxGeometryKind.BackgroundAnchor => "BGO", SmbxGeometryKind.NpcAnchor => "NPC",
            SmbxGeometryKind.Physics => "PHYSICS", SmbxGeometryKind.WarpEntrance => "DOORS",
            _ => throw new FormatException("This source kind is not a palette object.")
        };
        if (IsLegacy && !_insertions.ContainsKey(section))
            throw new FormatException($"Format version {FormatVersion} has no {section} object list.");
        var fields = new List<(string Name, string Value)>(); int version = FormatVersion;
        void N(string name, double value) => fields.Add((name, value.ToString("R", CultureInfo.InvariantCulture)));
        void B(string name, bool value = false) => fields.Add((name, IsLegacy ? value ? "#TRUE#" : "#FALSE#" : value ? "1" : "0"));
        void S(string name, string value = "") => fields.Add((name, IsLegacy ? EncodeLegacyString(value) : EncodeString(value)));
        if (section == "DOORS")
        {
            N("IX", bounds.X); N("IY", bounds.Y); N("OX", bounds.X + 160); N("OY", bounds.Y);
            // The specification's entrance/exit direction enums differ: 3 means
            // enter down and emerge up respectively. DT=1 is a pipe warp.
            N("ID", 3); N("OD", 3); N("DT", 1);
            if (!IsLegacy || version >= 3) { S("LF"); N("LI", 0); B("ET"); }
            if (!IsLegacy || version >= 4) { B("EX"); N("WX", -1); N("WY", -1); }
            if (!IsLegacy || version >= 7) N("SL", 0);
            if (!IsLegacy || version >= 12) { S("LR", "Default"); if (IsLegacy) B("LEGACY_HIDDEN"); }
            if (!IsLegacy || version >= 23) B("NV");
            if (!IsLegacy || version >= 25) B("AI", true);
            if (!IsLegacy || version >= 26) B("LC");
        }
        else
        {
            N("X", bounds.X); N("Y", bounds.Y);
            if (section == "BLOCK")
            {
                // Legacy stores height before width; LVLX field names are explicit.
                N("H", bounds.Height); N("W", bounds.Width); N("ID", id);
                N(IsLegacy ? "LEGACY_CONTENT" : "CN", 0); B("IV");
                if (!IsLegacy || version >= 61) B("SL");
                if (!IsLegacy || version >= 10) S("LR", "Default");
                if (!IsLegacy || version >= 14) { S("ED"); S("EH"); S("EE"); }
            }
            else if (section == "BGO")
            { N("ID", id); if (!IsLegacy || version >= 10) S("LR", "Default"); }
            else if (section == "NPC")
            {
                N("D", -1); N("ID", id);
                if (!IsLegacy || LegacyNpcHasSpecial(id, version)) N("S1", 0);
                if (!IsLegacy || version >= 3) B("GE");
                if (!IsLegacy || version >= 5) S("MG");
                if (!IsLegacy || version >= 6) { B("FD"); B("NM"); }
                if (!IsLegacy || version >= 9) B("BS");
                if (!IsLegacy || version >= 10) { S("LR", "Default"); S("EA"); S("ED"); S("ET"); }
                if (!IsLegacy || version >= 14) S("EE");
                if (!IsLegacy || version >= 63) S("LA");
            }
            else
            {
                N("W", bounds.Width); N("H", bounds.Height);
                if (IsLegacy) { N("LEGACY_BUOY", 0); if (version >= 62) B("LEGACY_QUICKSAND"); }
                else N("ET", 0);
                S("LR", "Default");
            }
        }
        int newline = _text.IndexOfAny(['\r', '\n']);
        string eol = newline >= 0 && _text[newline] == '\n' ? "\n" : newline >= 0 && newline + 1 < _text.Length && _text[newline + 1] != '\n' ? "\r" : "\r\n";
        var row = new StringBuilder();
        foreach (var field in fields)
        {
            if (!IsLegacy) row.Append(field.Name).Append(':');
            row.Append(field.Value).Append(IsLegacy ? eol : ";");
        }
        if (!IsLegacy) row.Append(eol);
        if (_insertions.TryGetValue(section, out int offset))
        {
            bool endsLine = offset > 0 && _text[offset - 1] is '\r' or '\n';
            bool indentedDelimiter = offset < _text.Length && offset > 0 && _text[offset - 1] is ' ' or '\t';
            string prefix = offset > 0 && !endsLine && !indentedDelimiter ? eol : "";
            return new(offset, "", prefix + row);
        }
        string separator = _text.Length > 0 && _text[^1] is not ('\r' or '\n') ? eol : "";
        return new(_text.Length, "", separator + section + eol + row + section + "_END" + eol);
    }
}
