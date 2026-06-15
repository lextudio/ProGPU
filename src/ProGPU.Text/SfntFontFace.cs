using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ProGPU.Text;

public static class SfntNameIds
{
    public const ushort FamilyName = 1;
    public const ushort SubfamilyName = 2;
    public const ushort UniqueFontIdentifier = 3;
    public const ushort FullName = 4;
    public const ushort Version = 5;
    public const ushort PostScriptName = 6;
    public const ushort PreferredFamilyName = 16;
    public const ushort PreferredSubfamilyName = 17;
}

public readonly struct SfntTableRecord
{
    public SfntTableRecord(string tag, uint checksum, uint offset, uint length)
    {
        Tag = tag;
        Checksum = checksum;
        Offset = offset;
        Length = length;
    }

    public string Tag { get; }
    public uint Checksum { get; }
    public uint Offset { get; }
    public uint Length { get; }
}

public sealed class SfntFontFace
{
    private readonly byte[] _data;
    private readonly Dictionary<string, SfntTableRecord> _tables;

    private SfntFontFace(byte[] data, int faceIndex, uint baseOffset, Dictionary<string, SfntTableRecord> tables)
    {
        _data = data;
        FaceIndex = faceIndex;
        BaseOffset = baseOffset;
        _tables = tables;
    }

    public int FaceIndex { get; }
    public uint BaseOffset { get; }
    public IReadOnlyDictionary<string, SfntTableRecord> Tables => _tables;

    public static SfntFontFace Load(string filePath, int faceIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return Load(File.ReadAllBytes(filePath), faceIndex);
    }

    public static SfntFontFace Load(byte[] fontData, int faceIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(fontData);
        IReadOnlyList<SfntFontFace> faces = LoadFaces(fontData);
        if ((uint)faceIndex >= (uint)faces.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(faceIndex));
        }

        return faces[faceIndex];
    }

    public static IReadOnlyList<SfntFontFace> LoadFaces(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return LoadFaces(File.ReadAllBytes(filePath));
    }

    public static IReadOnlyList<SfntFontFace> LoadFaces(byte[] fontData)
    {
        ArgumentNullException.ThrowIfNull(fontData);

        uint[] faceOffsets = ReadFaceOffsets(fontData);
        var faces = new List<SfntFontFace>(faceOffsets.Length);
        for (int i = 0; i < faceOffsets.Length; i++)
        {
            faces.Add(ParseFace(fontData, i, faceOffsets[i]));
        }

        return faces;
    }

    public static bool TryLoadFaces(string filePath, out IReadOnlyList<SfntFontFace> faces)
    {
        try
        {
            faces = LoadFaces(filePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException || ex is ArgumentException)
        {
            faces = Array.Empty<SfntFontFace>();
            return false;
        }
    }

    public bool TryGetTable(string tag, out ReadOnlyMemory<byte> table)
    {
        ArgumentNullException.ThrowIfNull(tag);

        if (_tables.TryGetValue(tag, out SfntTableRecord record) &&
            record.Offset <= _data.Length &&
            record.Length <= _data.Length - record.Offset)
        {
            table = _data.AsMemory((int)record.Offset, (int)record.Length);
            return true;
        }

        table = default;
        return false;
    }

    public IReadOnlyList<string> GetNames(ushort nameId)
    {
        if (!TryGetTable("name", out ReadOnlyMemory<byte> tableMemory))
        {
            return Array.Empty<string>();
        }

        ReadOnlySpan<byte> table = tableMemory.Span;
        if (table.Length < 6)
        {
            return Array.Empty<string>();
        }

        ushort count = ReadUShort(table, 2);
        ushort stringOffset = ReadUShort(table, 4);
        int recordsEnd = 6 + count * 12;
        if (recordsEnd > table.Length || stringOffset > table.Length)
        {
            return Array.Empty<string>();
        }

        var candidates = new List<NameCandidate>();
        for (int i = 0; i < count; i++)
        {
            int recordOffset = 6 + i * 12;
            ushort platformId = ReadUShort(table, recordOffset);
            ushort encodingId = ReadUShort(table, recordOffset + 2);
            ushort languageId = ReadUShort(table, recordOffset + 4);
            ushort recordNameId = ReadUShort(table, recordOffset + 6);
            ushort length = ReadUShort(table, recordOffset + 8);
            ushort offset = ReadUShort(table, recordOffset + 10);

            if (recordNameId != nameId)
            {
                continue;
            }

            int valueOffset = stringOffset + offset;
            if (valueOffset > table.Length || length > table.Length - valueOffset)
            {
                continue;
            }

            string value = DecodeName(table.Slice(valueOffset, length), platformId, encodingId);
            if (!string.IsNullOrWhiteSpace(value))
            {
                candidates.Add(new NameCandidate(value, GetNameScore(platformId, languageId)));
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .Select(candidate => candidate.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryGetName(ushort nameId, out string name)
    {
        IReadOnlyList<string> names = GetNames(nameId);
        if (names.Count > 0)
        {
            name = names[0];
            return true;
        }

        name = string.Empty;
        return false;
    }

    private static uint[] ReadFaceOffsets(byte[] data)
    {
        if (data.Length < 12)
        {
            throw new FormatException("Font data is too short to contain an SFNT header.");
        }

        if (ReadTag(data, 0) != "ttcf")
        {
            return new[] { 0u };
        }

        uint faceCount = ReadUInt(data, 8);
        if (faceCount == 0 || faceCount > int.MaxValue)
        {
            throw new FormatException("TrueType collection has an invalid face count.");
        }

        int offsetsLength = checked((int)faceCount * 4);
        if (12 + offsetsLength > data.Length)
        {
            throw new FormatException("TrueType collection offset table is truncated.");
        }

        var offsets = new uint[faceCount];
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = ReadUInt(data, 12 + i * 4);
        }

        return offsets;
    }

    private static SfntFontFace ParseFace(byte[] data, int faceIndex, uint baseOffset)
    {
        if (baseOffset > data.Length || data.Length - baseOffset < 12)
        {
            throw new FormatException("SFNT face header is outside the font data.");
        }

        ushort tableCount = ReadUShort(data, checked((int)baseOffset + 4));
        int directoryOffset = checked((int)baseOffset + 12);
        int directoryLength = checked(tableCount * 16);
        if (directoryOffset > data.Length || directoryLength > data.Length - directoryOffset)
        {
            throw new FormatException("SFNT table directory is truncated.");
        }

        var tables = new Dictionary<string, SfntTableRecord>(StringComparer.Ordinal);
        for (int i = 0; i < tableCount; i++)
        {
            int recordOffset = directoryOffset + i * 16;
            string tag = ReadTag(data, recordOffset);
            uint checksum = ReadUInt(data, recordOffset + 4);
            uint tableOffset = ReadUInt(data, recordOffset + 8);
            uint tableLength = ReadUInt(data, recordOffset + 12);

            if (tableOffset > data.Length || tableLength > data.Length - tableOffset)
            {
                continue;
            }

            tables[tag] = new SfntTableRecord(tag, checksum, tableOffset, tableLength);
        }

        return new SfntFontFace(data, faceIndex, baseOffset, tables);
    }

    private static string DecodeName(ReadOnlySpan<byte> bytes, ushort platformId, ushort encodingId)
    {
        string value;
        if (platformId == 0 || platformId == 3)
        {
            value = Encoding.BigEndianUnicode.GetString(bytes);
        }
        else if (platformId == 1 && encodingId == 0)
        {
            value = Encoding.Latin1.GetString(bytes);
        }
        else
        {
            value = Encoding.UTF8.GetString(bytes);
        }

        return value.Replace("\0", string.Empty).Trim();
    }

    private static int GetNameScore(ushort platformId, ushort languageId)
    {
        if (platformId == 3 && languageId == 0x0409)
        {
            return 4;
        }

        if (platformId == 3)
        {
            return 3;
        }

        if (platformId == 0)
        {
            return 2;
        }

        return 1;
    }

    private static string ReadTag(byte[] data, int offset)
    {
        if (offset > data.Length || data.Length - offset < 4)
        {
            throw new FormatException("SFNT tag is truncated.");
        }

        return new string(new[]
        {
            (char)data[offset],
            (char)data[offset + 1],
            (char)data[offset + 2],
            (char)data[offset + 3],
        });
    }

    private static ushort ReadUShort(byte[] data, int offset)
    {
        if (offset > data.Length || data.Length - offset < 2)
        {
            throw new FormatException("SFNT UInt16 value is truncated.");
        }

        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private static uint ReadUInt(byte[] data, int offset)
    {
        if (offset > data.Length || data.Length - offset < 4)
        {
            throw new FormatException("SFNT UInt32 value is truncated.");
        }

        return (uint)((data[offset] << 24) |
                      (data[offset + 1] << 16) |
                      (data[offset + 2] << 8) |
                       data[offset + 3]);
    }

    private static ushort ReadUShort(ReadOnlySpan<byte> data, int offset)
    {
        if (offset > data.Length || data.Length - offset < 2)
        {
            throw new FormatException("SFNT UInt16 value is truncated.");
        }

        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private readonly struct NameCandidate
    {
        public NameCandidate(string value, int score)
        {
            Value = value;
            Score = score;
        }

        public string Value { get; }
        public int Score { get; }
    }
}
