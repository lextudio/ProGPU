using System.IO.Compression;

namespace ProGPU.GameEngine.Assets;

/// <summary>
/// Owned ZIP bytes and an index, without eager expansion. Initialization costs O(C + E log E)
/// for copied archive bytes C and entries E; a requested file costs O(U) decompression
/// and CRC validation for U expanded bytes. Unrequested entries do not decompress.
/// No expanded cache is retained; callers own each returned byte array. Reads are
/// serialized because the BCL archive shares one seekable input stream. Disposal
/// releases archive/index references; already returned content remains valid.
/// </summary>
public sealed class IndexedAssetArchive : IAssetSource, IDisposable
{
    public const int MaximumArchiveBytes = 128 * 1024 * 1024;
    public const int MaximumEntries = 16_384;
    public const long MaximumExpandedBytes = 1024L * 1024 * 1024;
    private readonly object _gate = new();
    private readonly Dictionary<string, ZipArchiveEntry> _entries = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<string> _paths;
    private MemoryStream? _input;
    private ZipArchive? _archive;
    public IEnumerable<string> Paths => _paths;
    public long DeclaredExpandedBytes { get; }
    public int EncodedBytes { get; }
    public long ReadCount { get; private set; }
    public long ExpandedReadBytes { get; private set; }

    private IndexedAssetArchive(byte[] owned)
    {
        AssetBundle.Preflight(owned, MaximumEntries, MaximumExpandedBytes);
        EncodedBytes = owned.Length;
        _input = new MemoryStream(owned, writable: false);
        try
        {
            _archive = new(_input, ZipArchiveMode.Read, leaveOpen: true);
            if (_archive.Entries.Count > MaximumEntries) throw new FormatException("Too many indexed ZIP entries.");
            long total = 0;
            foreach (var entry in _archive.Entries)
            {
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                { if (entry.Length != 0) throw new FormatException("ZIP directories cannot contain file data."); continue; }
                string path = AssetBundle.ResolvePath("", entry.FullName);
                if (((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000) throw new FormatException("ZIP symbolic links are not supported.");
                if (entry.Length < 0 || entry.Length > AssetBundle.MaximumFileBytes || total + entry.Length > MaximumExpandedBytes)
                    throw new FormatException("Indexed ZIP content exceeds its eight-MiB-file or one-GiB-expanded limit.");
                total += entry.Length;
                if (!_entries.TryAdd(path, entry)) throw new FormatException($"Duplicate ZIP path '{path}'.");
            }
            DeclaredExpandedBytes = total;
            _paths = Array.AsReadOnly(_entries.Keys.Order(StringComparer.Ordinal).ToArray());
        }
        catch (Exception error)
        {
            _archive?.Dispose(); _input?.Dispose(); _archive = null; _input = null; _entries.Clear();
            if (error is InvalidDataException or EndOfStreamException) throw new FormatException("Malformed indexed ZIP archive.", error);
            throw;
        }
    }

    public static IndexedAssetArchive Open(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MaximumArchiveBytes) throw new FormatException("Indexed ZIP archives support at most 128 MiB.");
        return new(bytes.ToArray());
    }

    /// <summary>Reads the remaining seekable source once into owned storage; leaves the caller's stream open.</summary>
    public static async Task<IndexedAssetArchive> ReadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (!source.CanRead || !source.CanSeek) throw new ArgumentException("Archive input must be a readable, seekable stream.", nameof(source));
        long remaining = source.Length - source.Position;
        if (remaining < 0 || remaining > MaximumArchiveBytes) throw new FormatException("Indexed ZIP archives support at most 128 MiB.");
        byte[] owned = new byte[(int)remaining]; await source.ReadExactlyAsync(owned, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(owned);
    }

    public bool TryGet(string path, out ReadOnlyMemory<byte> bytes)
    {
        string name = AssetBundle.ResolvePath("", path);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_archive is null, this);
            if (!_entries.TryGetValue(name, out var entry)) { bytes = default; return false; }
            var content = new byte[(int)entry.Length];
            try
            {
                using var stream = entry.Open(); stream.ReadExactly(content);
                if (stream.ReadByte() != -1) throw new FormatException("Expanded ZIP entry exceeds its declared size.");
                AssetBundle.ValidateCrc(content, entry.Crc32, name);
            }
            catch (InvalidDataException error) { throw new FormatException($"Invalid ZIP content for '{name}'.", error); }
            catch (EndOfStreamException error) { throw new FormatException($"Truncated ZIP content for '{name}'.", error); }
            ReadCount++; ExpandedReadBytes += content.Length; bytes = content; return true;
        }
    }
    public ReadOnlyMemory<byte> Get(string path) => TryGet(path, out var bytes) ? bytes : throw new FormatException($"ZIP file '{path}' is missing.");

    public void Dispose()
    {
        lock (_gate)
        {
            _archive?.Dispose(); _input?.Dispose(); _archive = null; _input = null; _entries.Clear();
        }
    }
}
