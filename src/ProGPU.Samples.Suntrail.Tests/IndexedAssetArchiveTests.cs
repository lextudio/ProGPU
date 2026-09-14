using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ProGPU.GameEngine.Assets;
using ProGPU.Samples.Suntrail.Game.Import;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class IndexedAssetArchiveTests
{
    [Fact]
    public void EagerAndIndexedSourcesReturnIdenticalFileBytesForTheSharedContract()
    {
        var source = AssetBundle.FromFiles([new("levels/test.lvlx", "original fixture\r\n"u8.ToArray()),
            new("art/pixels.bin", new byte[] { 0, 255, 17, 128 }), new("empty.txt", ReadOnlyMemory<byte>.Empty)]);
        byte[] zip = source.WriteZip(); var eager = AssetBundle.FromZip(zip); using var indexed = IndexedAssetArchive.Open(zip);
        Assert.Equal(eager.Paths.Order(StringComparer.Ordinal), indexed.Paths);
        foreach (string path in eager.Paths) Assert.Equal(eager.Get(path).ToArray(), indexed.Get(path).ToArray());
    }

    [Fact]
    public void DefinitionCacheEvictsByFieldBudgetAndReloadsFromImmutableArchive()
    {
        var text = new StringBuilder("[npc]\nphysical-width=23\n");
        for (int i = 0; i < 2000; i++) text.Append("original-field-").Append(i).Append("=1\n");
        byte[] source = Encoding.UTF8.GetBytes(text.ToString());
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            using (var entry = zip.CreateEntry("main.ini").Open()) entry.Write("[main]\napplication-dir=0\n"u8);
            using (var entry = zip.CreateEntry("lvl_npc.ini").Open()) entry.Write("[npc-main]\nconfig-dir=actors\n"u8);
            for (int id = 1; id <= 35; id++) using (var entry = zip.CreateEntry($"actors/npc-{id}.ini").Open()) entry.Write(source);
        }
        using var archive = IndexedAssetArchive.Open(output.ToArray());
        using var pack = new SmbxConfigPack(archive, "main.ini");
        var first = pack.Get(SmbxArtworkKind.Npc, 1);
        for (int id = 2; id <= 35; id++) pack.Get(SmbxArtworkKind.Npc, id);
        Assert.InRange(pack.CachedFieldCount, 1, 65_536); Assert.InRange(pack.CachedSourceBytes, 1, 16 * 1024 * 1024);
        long reads = archive.ReadCount; pack.Get(SmbxArtworkKind.Npc, 35); Assert.Equal(reads, archive.ReadCount);
        Assert.NotSame(first, pack.Get(SmbxArtworkKind.Npc, 1)); Assert.Equal(reads + 1, archive.ReadCount);
    }

    [Fact]
    public void LargeDefinitionPackDoesNotExpandUnrequestedFiles()
    {
        byte[] encoded = ConfigZip(includeUnusedPayloads: true);
        Assert.Throws<FormatException>(() => AssetBundle.FromZip(encoded));
        using var archive = IndexedAssetArchive.Open(encoded);
        Assert.Equal(0, archive.ReadCount); Assert.True(archive.DeclaredExpandedBytes > AssetBundle.MaximumExpandedBytes);
        using var definitions = new SmbxConfigPack(archive, "base/main.ini");
        Assert.Equal(2, archive.ReadCount); // Main and NPC index only.
        var npc = definitions.Get(SmbxArtworkKind.Npc, 173)!;
        Assert.Equal(23, npc.PhysicalWidth); Assert.Equal(41, npc.PhysicalHeight);
        Assert.Equal(3, archive.ReadCount); Assert.Same(npc, definitions.Get(SmbxArtworkKind.Npc, 173));
        Assert.Equal(3, archive.ReadCount); Assert.InRange(archive.ExpandedReadBytes, 1, 1024);
        Assert.True(definitions.CachedSourceBytes > 0); Assert.Equal(2, definitions.CachedFieldCount);
    }

    [Fact]
    public void ReadsOwnOutputAndSourceDisposalDoesNotInvalidateReturnedContent()
    {
        byte[] encoded = ConfigZip(); using var archive = IndexedAssetArchive.Open(encoded);
        Array.Fill(encoded, (byte)0);
        var first = archive.Get("base/main.ini"); var second = archive.Get("base/main.ini");
        Assert.Equal(first.ToArray(), second.ToArray()); Assert.Equal(2, archive.ReadCount);
        Assert.False(archive.TryGet("missing", out _)); Assert.Equal(2, archive.ReadCount);
        archive.Dispose(); Assert.Equal("[main]\napplication-dir=0\n", Encoding.UTF8.GetString(first.Span));
        Assert.Throws<ObjectDisposedException>(() => archive.Get("base/main.ini"));
    }

    [Fact]
    public async Task StreamLoadingOwnsOneBufferAndRespectsCancellation()
    {
        using var stream = new MemoryStream(ConfigZip());
        using var archive = await IndexedAssetArchive.ReadAsync(stream);
        Assert.True(stream.CanRead); Assert.Equal(stream.Length, stream.Position); Assert.Equal(0, archive.ReadCount);
        stream.Position = 0; using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => IndexedAssetArchive.ReadAsync(stream, cancel.Token));
    }

    [Fact]
    public void EntryChecksumIsCheckedOnlyWhenThatEntryIsRequested()
    {
        byte[] bytes = ConfigZip();
        for (int i = 0; i < bytes.Length - 46; i++)
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i)) == 0x02014b50)
            { bytes[i + 16] ^= 1; break; } // Corrupt only the first entry's central CRC.
        using var archive = IndexedAssetArchive.Open(bytes); Assert.Equal(0, archive.ReadCount);
        Assert.NotEmpty(archive.Get("base/actors/npc-173.ini").ToArray());
        Assert.Throws<FormatException>(() => archive.Get("base/main.ini"));
        Assert.Equal(1, archive.ReadCount);
    }

    [Fact]
    public void ArchiveIndexRejectsEscapingDuplicatesAndExcessiveEntryCounts()
    {
        byte[] Zip(params string[] names)
        {
            using var output = new MemoryStream();
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
                foreach (string name in names) using (var entry = archive.CreateEntry(name).Open()) entry.WriteByte(1);
            return output.ToArray();
        }
        Assert.Throws<FormatException>(() => IndexedAssetArchive.Open(Zip("../outside")));
        Assert.Throws<FormatException>(() => IndexedAssetArchive.Open(Zip("a/../same", "same")));
        byte[] bytes = Zip("one"); int end = bytes.Length - 22;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(end + 8), IndexedAssetArchive.MaximumEntries + 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(end + 10), IndexedAssetArchive.MaximumEntries + 1);
        Assert.Throws<FormatException>(() => IndexedAssetArchive.Open(bytes));
    }

    [Fact]
    public void ConfigPackOwnershipIsExplicitAndCachedFailuresAvoidRepeatedInflation()
    {
        using var archive = IndexedAssetArchive.Open(ConfigZip(malformed: true));
        using var borrowed = new SmbxConfigPack(archive, "base/main.ini");
        Assert.Throws<FormatException>(() => borrowed.Get(SmbxArtworkKind.Npc, 173)); long reads = archive.ReadCount;
        Assert.Throws<FormatException>(() => borrowed.Get(SmbxArtworkKind.Npc, 173)); Assert.Equal(reads, archive.ReadCount);
        borrowed.Dispose(); Assert.NotEmpty(archive.Get("base/main.ini").ToArray());
        var owner = new SmbxConfigPack(archive, "base/main.ini", ownsSource: true); owner.Dispose();
        Assert.Throws<ObjectDisposedException>(() => archive.Get("base/main.ini"));
    }

    private static byte[] ConfigZip(bool includeUnusedPayloads = false, bool malformed = false)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            void Text(string path, string value)
            { using var entry = zip.CreateEntry(path, CompressionLevel.Optimal).Open(); entry.Write(Encoding.UTF8.GetBytes(value)); }
            Text("base/main.ini", "[main]\napplication-dir=0\n");
            Text("base/lvl_npc.ini", "[npc-main]\nconfig-dir=actors\n");
            for (int id = 1; id <= 220; id++) Text($"base/actors/npc-{id}.ini", malformed && id == 173 ? "not an ini" : "[npc]\nphysical-width=23\nphysical-height=41\n");
            if (includeUnusedPayloads)
            {
                byte[] block = new byte[1024 * 1024];
                for (int file = 0; file < 10; file++)
                { using var entry = zip.CreateEntry($"unused/data-{file}.bin", CompressionLevel.Optimal).Open(); for (int part = 0; part < 4; part++) entry.Write(block); }
            }
        }
        return output.ToArray();
    }
}
