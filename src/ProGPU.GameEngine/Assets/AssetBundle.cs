using System.Buffers.Binary;
using System.IO.Compression;

namespace ProGPU.GameEngine.Assets;

/// <summary>
/// Owned, bounded package bytes with package-relative references. No filesystem,
/// URL loading or extraction. Loading costs O(C + U + E) for compressed bytes C,
/// expanded bytes U and entries E; storage is O(C + U + E). All work is import-time.
/// Standard stored/deflated single-disk ZIPs use the BCL decompressor; original
/// preflight and CRC checks follow PKWARE's public record/polynomial contracts.
/// </summary>
public sealed partial class AssetBundle : IAssetSource
{
    public const int MaximumArchiveBytes = 16 * 1024 * 1024;
    public const int MaximumExpandedBytes = 32 * 1024 * 1024;
    public const int MaximumFileBytes = 8 * 1024 * 1024;
    public const int MaximumEntries = 128;
    private readonly Dictionary<string, byte[]> _files;
    private static readonly uint[] CrcTable = CreateCrcTable();
    public IEnumerable<string> Paths => _files.Keys;
    public long ExpandedBytes { get; }

    private AssetBundle(Dictionary<string, byte[]> files, long bytes) { _files = files; ExpandedBytes = bytes; }

    public static AssetBundle FromFiles(IEnumerable<KeyValuePair<string, ReadOnlyMemory<byte>>> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var owned = new Dictionary<string, byte[]>(StringComparer.Ordinal); long total = 0;
        foreach (var file in files)
        {
            if (owned.Count == MaximumEntries) throw new FormatException("A package supports at most 128 files.");
            string name = ResolvePath("", file.Key);
            if (owned.ContainsKey(name)) throw new FormatException($"Duplicate package path '{name}'.");
            total = CheckSize(file.Value.Length, total);
            owned.Add(name, file.Value.ToArray());
        }
        return new(owned, total);
    }

    public bool TryGet(string path, out ReadOnlyMemory<byte> bytes)
    {
        if (_files.TryGetValue(ResolvePath("", path), out var file)) { bytes = file; return true; }
        bytes = default; return false;
    }

    public ReadOnlyMemory<byte> Get(string path) => TryGet(path, out var bytes) ? bytes :
        throw new FormatException($"Package file '{path}' is missing.");

    /// <summary>Resolve a relative reference against the containing file, within the package root.</summary>
    public static string ResolvePath(string containingFile, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 512 || reference.Any(char.IsControl))
            throw new FormatException("Package references need a printable relative path of at most 512 characters.");
        reference = reference.Replace('\\', '/');
        if (reference.StartsWith('/') || reference.Contains(':')) throw new FormatException("Package references must be relative, without URLs or drive names.");
        string directory = "";
        if (containingFile.Length != 0)
        {
            string normalized = ResolvePath("", containingFile);
            int slash = normalized.LastIndexOf('/');
            if (slash >= 0) directory = normalized[..(slash + 1)];
        }
        var segments = new List<string>();
        foreach (string segment in (directory + reference).Split('/'))
        {
            if (segment is "" or ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0) throw new FormatException("A package reference leaves the package root.");
                segments.RemoveAt(segments.Count - 1);
            }
            else segments.Add(segment);
        }
        string result = string.Join('/', segments);
        if (result.Length is 0 or > 512) throw new FormatException("The resolved package path is empty or too long.");
        return result;
    }

    public static AssetBundle FromZip(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MaximumArchiveBytes) throw new FormatException("ZIP packages must be at most 16 MiB.");
        Preflight(bytes.Span);
        using var input = new MemoryStream(bytes.ToArray(), false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, false);
        if (archive.Entries.Count > MaximumEntries) throw new FormatException("A package supports at most 128 ZIP entries.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal); long total = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                if (entry.Length != 0) throw new FormatException("ZIP directories cannot contain file data.");
                continue;
            }
            string name = ResolvePath("", entry.FullName);
            if (files.ContainsKey(name)) throw new FormatException($"Duplicate package path '{name}'.");
            if (((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000) throw new FormatException("Package symbolic links are not supported.");
            total = CheckSize(entry.Length, total);
            var content = new byte[(int)entry.Length];
            using var stream = entry.Open(); stream.ReadExactly(content);
            if (stream.ReadByte() != -1) throw new FormatException("Expanded ZIP entry exceeds its declared size.");
            ValidateCrc(content, entry.Crc32, name);
            files.Add(name, content);
        }
        return new(files, total);
    }

    private static long CheckSize(long size, long total)
    {
        if (size < 0 || size > MaximumFileBytes || total + size > MaximumExpandedBytes)
            throw new FormatException("Packages support 8 MiB per file and 32 MiB of expanded content.");
        return total + size;
    }

    internal static void Preflight(ReadOnlySpan<byte> bytes, int maximumEntries = MaximumEntries,
        long maximumExpandedBytes = MaximumExpandedBytes, int maximumFileBytes = MaximumFileBytes)
    {
        // Bound central-directory object creation before asking ZipArchive to parse it.
        // These offsets are ZIP wire fields, not a copied implementation or lookup table.
        int end = -1;
        for (int i = bytes.Length - 22; i >= Math.Max(0, bytes.Length - 65_557); i--)
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes[i..]) == 0x06054b50 &&
                i + 22 + BinaryPrimitives.ReadUInt16LittleEndian(bytes[(i + 20)..]) == bytes.Length) { end = i; break; }
        if (end < 0) throw new FormatException("Missing ZIP directory terminator.");
        var record = bytes[end..];
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(record[10..]);
        if (BinaryPrimitives.ReadUInt32LittleEndian(record[4..]) != 0 || count > maximumEntries ||
            count != BinaryPrimitives.ReadUInt16LittleEndian(record[8..]))
            throw new FormatException($"Packages require a single-disk ZIP with at most {maximumEntries} entries; ZIP64 is not supported.");
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(record[12..]);
        uint start = BinaryPrimitives.ReadUInt32LittleEndian(record[16..]);
        if ((ulong)start + length != (ulong)end) throw new FormatException("Invalid ZIP central-directory bounds.");
        int position = (int)start; long total = 0;
        for (int i = 0; i < count; i++)
        {
            if (end - position < 46 || BinaryPrimitives.ReadUInt32LittleEndian(bytes[position..]) != 0x02014b50)
                throw new FormatException("Invalid ZIP central-directory entry.");
            var header = bytes[position..];
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header[24..]);
            if (size > maximumFileBytes || total + size > maximumExpandedBytes)
                throw new FormatException("ZIP content exceeds the configured per-file or expanded-byte limit.");
            total += size;
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(header[8..]);
            ushort method = BinaryPrimitives.ReadUInt16LittleEndian(header[10..]);
            if ((flags & 0x2041) != 0 || method is not (0 or 8) || BinaryPrimitives.ReadUInt16LittleEndian(header[34..]) != 0)
                throw new FormatException("Packages support unencrypted stored or deflated ZIP entries on one disk.");
            int name = BinaryPrimitives.ReadUInt16LittleEndian(header[28..]);
            int extra = BinaryPrimitives.ReadUInt16LittleEndian(header[30..]);
            int comment = BinaryPrimitives.ReadUInt16LittleEndian(header[32..]);
            long next = (long)position + 46 + name + extra + comment;
            if (next > end) throw new FormatException("Truncated ZIP central-directory entry.");
            position = (int)next;
        }
        if (position != end) throw new FormatException("ZIP directory entry count does not match its contents.");
    }

    internal static void ValidateCrc(ReadOnlySpan<byte> bytes, uint expected, string name)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes) crc = (crc >> 8) ^ CrcTable[(crc ^ value) & 255];
        if (~crc != expected) throw new FormatException($"ZIP checksum failed for '{name}'.");
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            uint value = i;
            for (int bit = 0; bit < 8; bit++) value = (value >> 1) ^ ((value & 1) == 0 ? 0 : 0xedb88320u);
            table[i] = value;
        }
        return table;
    }
}
