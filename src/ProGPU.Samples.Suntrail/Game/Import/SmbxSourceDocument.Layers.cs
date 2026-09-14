using System.Text;

namespace ProGPU.Samples.Suntrail.Game.Import;

public sealed partial class SmbxSourceDocument
{
    internal SmbxSourceSplice InsertLayer(string name)
    {
        if (IsLegacy && !_insertions.ContainsKey("LAYERS")) throw new FormatException("This legacy version has no layer declaration list.");
        string eol = _text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : _text.Contains('\n') ? "\n" : "\r";
        string row = IsLegacy ? EncodeLegacyString(name) + eol + "#FALSE#" + eol : "LR:" + EncodeString(name) + ";HD:0;LC:0;" + eol;
        if (_insertions.TryGetValue("LAYERS", out int offset))
        {
            string prefix = offset > 0 && _text[offset - 1] is not ('\r' or '\n' or ' ' or '\t') ? eol : "";
            return new(offset, "", prefix + row);
        }
        string separator = _text.Length > 0 && _text[^1] is not ('\r' or '\n') ? eol : "";
        return new(_text.Length, "", separator + "LAYERS" + eol + row + "LAYERS_END" + eol);
    }

    internal SmbxFieldEdit[] RenameLayerReferences(string previous, string next)
    {
        var edits = new List<SmbxFieldEdit>();
        string Encode(string value) => IsLegacy ? EncodeLegacyString(value) : EncodeString(value);
        for (int r = 0; r < _records.Length; r++)
        {
            var row = _records[r]; if (row.SectionPath != row.Section) continue;
            foreach (var field in row.Fields)
            {
                bool direct = field.Name == "LR" && row.Section is "LAYERS" or "BLOCK" or "BGO" or "NPC" or "PHYSICS" or "DOORS";
                direct |= row.Section == "NPC" && field.Name == "LA";
                bool classic = row.Section == "EVENTS_CLASSIC";
                direct |= classic && field.Name == "ML";
                if (classic && IsLegacy)
                    direct |= field.Name.Length > 2 && field.Name[..2] is "LH" or "LS" or "LT" && int.TryParse(field.Name.AsSpan(2), out _);
                string replacement = field.RawValue;
                if (direct)
                {
                    string value = field.GetString();
                    if (value == next) throw new FormatException("The destination layer name already exists or is referenced. Choose a distinct name.");
                    if (value == previous) replacement = Encode(next);
                }
                else if (classic && !IsLegacy && field.Name is "LH" or "LS" or "LT")
                    replacement = MapStringArray(field.RawValue, value => Rename(value));
                else if (classic && !IsLegacy && field.Name == "MLA")
                    replacement = MapStringArray(field.RawValue, value => RenameMovingEntry(value));
                if (replacement == field.RawValue) continue;
                if (edits.Count == 4096) throw new FormatException("This rename exceeds the 4096-field transaction limit.");
                edits.Add(new(r, field.Name, replacement));
            }
        }
        return edits.ToArray();

        string Rename(string value)
        {
            if (value == next) throw new FormatException("The destination layer is already referenced by a classic event.");
            return value == previous ? next : value;
        }
        string RenameMovingEntry(string value)
        {
            var fields = ParseFields(value, 0, value.Length, 0);
            foreach (var field in fields)
            {
                if (field.Name != "LN") continue;
                string original = field.GetString(), renamed = Rename(original);
                if (renamed == original) return value;
                return value[..field.Offset] + EncodeString(renamed) + value[(field.Offset + field.RawValue.Length)..];
            }
            return value;
        }
    }

    // Flat string arrays only. Preserve every unmodified element and separator,
    // including hex-encoded strings; reject nested/nonstring event operands.
    private static string MapStringArray(string raw, Func<string, string> map)
    {
        if (raw.Length < 2 || raw[0] != '[' || raw[^1] != ']') throw new FormatException("Classic layer actions require a string array.");
        var output = new StringBuilder(raw.Length); output.Append('[');
        int start = 1; bool quoted = false, escaped = false;
        for (int i = 1; i < raw.Length; i++)
        {
            char c = raw[i];
            if (quoted)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') quoted = false;
                continue;
            }
            if (c == '"') { quoted = true; continue; }
            if (c != ',' && i != raw.Length - 1) continue;
            string part = raw[start..i]; string token = part.Trim();
            if (token.Length == 0)
            {
                if (start != 1 || c == ',') throw new FormatException("Classic layer arrays cannot contain empty operands.");
                output.Append(part);
            }
            else
            {
                string value = DecodeString(token), replacement = map(value);
                if (replacement == value) output.Append(part);
                else
                {
                    int leading = part.Length - part.TrimStart().Length;
                    output.Append(part.AsSpan(0, leading)).Append(EncodeString(replacement)).Append(part.AsSpan(leading + token.Length));
                }
            }
            output.Append(c); start = i + 1;
        }
        return output.ToString();
    }
}
