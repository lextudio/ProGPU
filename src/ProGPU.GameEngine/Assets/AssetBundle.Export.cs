using System.IO.Compression;
using System.Text;

namespace ProGPU.GameEngine.Assets;

public sealed partial class AssetBundle
{
    /// <summary>
    /// Writes a bounded ZIP copy, replacing named existing files. Inputs are borrowed
    /// synchronously and must not change during this call. No package state is mutated.
    /// Entry order, timestamps and attributes are fixed; compression bytes are repeatable
    /// within the same runtime/compressor, not guaranteed identical between runtimes.
    /// O(U + E log E) preparation/compression for expanded bytes U and entries E, plus
    /// compressor scratch. Output buffer and returned owned copy are each at most 16 MiB.
    /// </summary>
    public byte[] WriteZip(IEnumerable<KeyValuePair<string, ReadOnlyMemory<byte>>>? replacements = null)
    {
        var files = ExportFiles(replacements);
        using var output = new BoundedZipOutput();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: Encoding.UTF8))
            foreach (var path in files.Keys.Order(StringComparer.Ordinal))
            {
                var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                entry.ExternalAttributes = 0;
                using var stream = entry.Open(); stream.Write(files[path].Span);
            }
        return output.ToArray();
    }

    /// <summary>Creates an immutable content revision, sharing unchanged owned files and copying replacements only.</summary>
    public AssetBundle WithReplacements(IEnumerable<KeyValuePair<string, ReadOnlyMemory<byte>>> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        var files = ExportFiles(replacements);
        var owned = new Dictionary<string, byte[]>(StringComparer.Ordinal); long total = 0;
        foreach (var file in files)
        {
            byte[] previous = _files[file.Key];
            owned.Add(file.Key, file.Value.Equals((ReadOnlyMemory<byte>)previous) ? previous : file.Value.ToArray());
            total += file.Value.Length;
        }
        return new(owned, total);
    }

    private Dictionary<string, ReadOnlyMemory<byte>> ExportFiles(IEnumerable<KeyValuePair<string, ReadOnlyMemory<byte>>>? replacements)
    {
        var files = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        foreach (var file in _files) files.Add(file.Key, file.Value);
        if (replacements is not null)
        {
            var replaced = new HashSet<string>(StringComparer.Ordinal);
            long total = ExpandedBytes;
            foreach (var replacement in replacements)
            {
                string path = ResolvePath("", replacement.Key);
                if (!files.TryGetValue(path, out var previous)) throw new FormatException($"Replacement '{path}' is not in this package.");
                if (!replaced.Add(path)) throw new FormatException($"Duplicate replacement '{path}'.");
                CheckSize(replacement.Value.Length, 0);
                total += replacement.Value.Length - previous.Length;
                files[path] = replacement.Value;
            }
            CheckSize(0, total); // Evaluate final size, independently of replacement enumeration order.
        }
        return files;
    }

    // Original write-only bounded stream adapter. ZipArchive owns ZIP encoding;
    // no headers, compression implementation or native archive library are copied.
    private sealed class BoundedZipOutput : Stream
    {
        private readonly MemoryStream _memory = new(4096);
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _memory.Length;
        public override long Position { get => _memory.Position; set => throw new NotSupportedException(); }
        public byte[] ToArray() => _memory.ToArray();
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            long required = _memory.Position + buffer.Length;
            if (required > MaximumArchiveBytes) throw new FormatException("Exported ZIP exceeds the 16 MiB package limit.");
            if (_memory.Capacity < required)
                _memory.Capacity = (int)Math.Min(MaximumArchiveBytes, Math.Max(required, (long)_memory.Capacity * 2));
            _memory.Write(buffer);
        }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void WriteByte(byte value) { Span<byte> one = stackalloc byte[1]; one[0] = value; Write(one); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _memory.Dispose(); base.Dispose(disposing); }
    }
}
