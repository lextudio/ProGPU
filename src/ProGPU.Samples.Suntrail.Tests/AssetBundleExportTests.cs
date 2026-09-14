using System.IO.Compression;
using System.Text;
using ProGPU.GameEngine.Assets;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class AssetBundleExportTests
{
    [Fact]
    public void ExportPreservesBinaryFilesAndNamesWithDeterministicMetadata()
    {
        var bundle = AssetBundle.FromFiles([new("episode/雪/npc-1.bmp", new byte[] { 0, 1, 255, 128 }),
            new("episode/yard.lvlx", "original"u8.ToArray()), new("episode/unused.lua", "retained; never executed"u8.ToArray())]);
        KeyValuePair<string, ReadOnlyMemory<byte>>[] edits = [new("episode/yard.lvlx", "edited\r\n"u8.ToArray())];
        byte[] first = bundle.WriteZip(edits); Assert.Equal(first, bundle.WriteZip(edits));
        var loaded = AssetBundle.FromZip(first);
        Assert.Equal("edited\r\n"u8.ToArray(), loaded.Get("episode/yard.lvlx").ToArray());
        Assert.Equal(bundle.Get("episode/雪/npc-1.bmp").ToArray(), loaded.Get("episode/雪/npc-1.bmp").ToArray());
        Assert.Equal(bundle.Get("episode/unused.lua").ToArray(), loaded.Get("episode/unused.lua").ToArray());
        Assert.Equal("original"u8.ToArray(), bundle.Get("episode/yard.lvlx").ToArray());
        using var archive = new ZipArchive(new MemoryStream(first), ZipArchiveMode.Read);
        Assert.Equal(archive.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal), archive.Entries.Select(e => e.FullName));
        foreach (var entry in archive.Entries)
        { Assert.Equal(1980, entry.LastWriteTime.Year); Assert.Equal(1, entry.LastWriteTime.Month); Assert.Equal(0, entry.ExternalAttributes); }
    }

    [Fact]
    public void ContentRevisionOwnsReplacementBytesAndRejectsMissingDuplicateOrEscapingNames()
    {
        var bundle = AssetBundle.FromFiles([new("map.lvlx", "old"u8.ToArray())]);
        byte[] replacement = "new"u8.ToArray();
        var next = bundle.WithReplacements([new("map.lvlx", replacement)]); replacement[0] = 0;
        Assert.Equal("new"u8.ToArray(), next.Get("map.lvlx").ToArray());
        Assert.Equal("old"u8.ToArray(), bundle.Get("map.lvlx").ToArray());
        Assert.Throws<FormatException>(() => bundle.WriteZip([new("missing", replacement)]));
        Assert.Throws<FormatException>(() => bundle.WriteZip([new("../map.lvlx", replacement)]));
        Assert.Throws<FormatException>(() => bundle.WriteZip([new("map.lvlx", replacement), new("./map.lvlx", replacement)]));
        Assert.Throws<FormatException>(() => bundle.WriteZip([new("map.lvlx", new byte[AssetBundle.MaximumFileBytes + 1])]));
    }

    [Fact]
    public void ExportSizeLimitAppliesWhileWritingCompressedOutput()
    {
        var random = new Random(1980);
        var files = new List<KeyValuePair<string, ReadOnlyMemory<byte>>>();
        for (int i = 0; i < 3; i++) { byte[] bytes = new byte[6 * 1024 * 1024]; random.NextBytes(bytes); files.Add(new($"data-{i}.bin", bytes)); }
        var bundle = AssetBundle.FromFiles(files);
        Assert.Throws<FormatException>(() => bundle.WriteZip());
        Assert.Equal(18 * 1024 * 1024, bundle.ExpandedBytes);
    }
}
