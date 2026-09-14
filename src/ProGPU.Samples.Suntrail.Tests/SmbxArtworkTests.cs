using System.Buffers.Binary;
using System.Text;
using ProGPU.GameEngine.Assets;
using ProGPU.Samples.Suntrail.Game.Import;
using ProGPU.Tests.Headless;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SmbxArtworkTests
{
    [Fact]
    public void BaseImagesResolveInsideGraphicsRootAndCustomArtWinsWithoutCrossSourceMasks()
    {
        using var definitions = BaseDefinitions("tile.bmp",
            ("base/art/blocks/tile.bmp", Bmp(2, 1, 48)),
            ("base/art/blocks/tilem.bmp", Bmp(2, 1, 0)));
        var baseOnly = new SmbxArtworkCatalog(Bundle(), "episode/yard.lvlx", definitions);
        var asset = Assert.IsType<SmbxArtwork>(baseOnly.Prepare(SmbxArtworkKind.Block, 17));
        Assert.True(asset.IsBaseArtwork); Assert.Equal("base/art/blocks/tile.bmp", asset.Path);
        Assert.Equal((byte)48, asset.Image.Pixels[0]); Assert.Equal((byte)255, asset.Image.Pixels[3]);
        Assert.Same(asset, baseOnly.Prepare(SmbxArtworkKind.Block, 17)); Assert.Equal(8, baseOnly.DecodedBytes);
        var episode = new SmbxArtworkCatalog(Bundle(("episode/block-17.bmp", Bmp(1, 1, 71))), "episode/yard.lvlx", definitions);
        var custom = Assert.IsType<SmbxArtwork>(episode.Prepare(SmbxArtworkKind.Block, 17));
        Assert.False(custom.IsBaseArtwork); Assert.Equal((byte)71, custom.Image.Pixels[0]);
        // An episode mask must not be paired with a base image, even with a matching basename.
        var isolated = new SmbxArtworkCatalog(Bundle(("episode/tilem.bmp", Bmp(2, 1, 255))), "episode/yard.lvlx", definitions);
        Assert.Equal((byte)255, isolated.Prepare(SmbxArtworkKind.Block, 17)!.Image.Pixels[3]);
        definitions.Dispose(); // Prepared pixels have no source/archive dependency.
        Assert.Equal((byte)48, asset.Image.Pixels[0]); Assert.Same(asset, baseOnly.Prepare(SmbxArtworkKind.Block, 17));
    }

    [Fact]
    public void BaseImageNamesRequireUniqueMatchUnlessExplicitPathOrRootImageExists()
    {
        var images = new[] { ("base/art/a/tile.bmp", Bmp(1, 1, 12)), ("base/art/b/tile.bmp", Bmp(1, 1, 24)) };
        using var ambiguous = BaseDefinitions("tile.bmp", images);
        var catalog = new SmbxArtworkCatalog(Bundle(), "yard.lvlx", ambiguous);
        var first = Assert.Throws<FormatException>(() => catalog.Prepare(SmbxArtworkKind.Block, 17));
        Assert.Contains("ambiguous", first.Message); Assert.Equal(0, catalog.DecodedBytes);
        Assert.Equal(first.Message, Assert.Throws<FormatException>(() => catalog.Prepare(SmbxArtworkKind.Block, 17)).Message);
        using var explicitPath = BaseDefinitions("B/TILE.BMP", images);
        Assert.Equal((byte)24, new SmbxArtworkCatalog(Bundle(), "yard.lvlx", explicitPath).Prepare(SmbxArtworkKind.Block, 17)!.Image.Pixels[0]);
        using var exactRoot = BaseDefinitions("tile.bmp", [.. images, ("base/art/tile.bmp", Bmp(1, 1, 36))]);
        Assert.Equal((byte)36, new SmbxArtworkCatalog(Bundle(), "yard.lvlx", exactRoot).Prepare(SmbxArtworkKind.Block, 17)!.Image.Pixels[0]);
    }

    [Theory]
    [InlineData("../outside.bmp")]
    [InlineData("/outside.bmp")]
    [InlineData("https://example.com/image.bmp")]
    public void BaseImageReferenceCannotEscapeItsExplicitGraphicsRoot(string reference)
    {
        using var definitions = BaseDefinitions(reference, ("base/outside.bmp", Bmp(1, 1, 12)));
        Assert.Throws<FormatException>(() => new SmbxArtworkCatalog(Bundle(), "yard.lvlx", definitions).Prepare(SmbxArtworkKind.Block, 17));
    }

    [Fact]
    public void BadCustomImageDoesNotFallBackToValidBaseAndMissingBaseIsReported()
    {
        using var definitions = BaseDefinitions("tile.bmp", ("base/art/tile.bmp", Bmp(1, 1, 12)));
        var catalog = new SmbxArtworkCatalog(Bundle(("yard/block-17.png", Text("broken"))), "yard.lvlx", definitions);
        Assert.Throws<FormatException>(() => catalog.Prepare(SmbxArtworkKind.Block, 17)); Assert.Equal(0, catalog.DecodedBytes);
        using var absent = BaseDefinitions("missing.bmp", ("base/unrelated/missing.bmp", Bmp(1, 1, 12)));
        Assert.Null(new SmbxArtworkCatalog(Bundle(), "yard.lvlx", absent).Prepare(SmbxArtworkKind.Block, 17));
    }

    [Fact]
    public void RefreshingDefinitionArtworkPreservesDraftBytesRevisionAndHistory()
    {
        var workshop = new SmbxPackageWorkshop(SmbxPackageWorkshopTests.Package());
        var draft = workshop.Open("episode/map-0.lvlx"); draft.Editor.Add(SmbxGeometryKind.Block, 17, 64, 96);
        int revision = draft.Editor.Revision; byte[] edited = draft.Editor.Document.WriteOriginal();
        using var definitions = BaseDefinitions("tile.bmp", ("base/art/tile.bmp", Bmp(1, 1, 12)));
        workshop.RefreshArtwork(definitions);
        Assert.Same(draft, workshop.Current); Assert.Equal(revision, draft.Editor.Revision);
        Assert.Equal(edited, draft.Editor.Document.WriteOriginal()); Assert.True(draft.HasUnsavedChanges);
        Assert.True(workshop.Artwork!.TryGetArtwork(SmbxArtworkKind.Block, 17, out var image)); Assert.True(image.IsBaseArtwork);
        Assert.Empty(workshop.Artwork.Issues.ToArray());
        draft.Editor.Undo(); draft.Editor.Redo(); Assert.Equal(edited, draft.Editor.Document.WriteOriginal());
        var exported = AssetBundle.FromZip(workshop.PrepareExport().CopyBytes());
        Assert.DoesNotContain(exported.Paths, p => p.StartsWith("base/", StringComparison.Ordinal));
    }

    private static SmbxConfigPack BaseDefinitions(string imageName, params (string Path, byte[] Bytes)[] images)
    {
        var files = new List<(string, byte[])>
        {
            ("base/MAIN.INI", Text("[main]\ngraphics-level=art")),
            ("base/LVL_BLOCKS.INI", Text("[blocks-main]\nconfig-dir=definitions")),
            ("base/definitions/BLOCK-17.INI", Text("[block]\nimage=\"" + imageName + "\""))
        };
        files.AddRange(images); return new SmbxConfigPack(Bundle(files.ToArray()), "base/main.ini");
    }

    [Fact]
    public void SectionBackgroundsPreferCurrentNamesAndKeepLevelSpecificLegacyFallback()
    {
        var modern = new SmbxArtworkCatalog(Bundle(("background2-3.bmp", Bmp(1, 1, 30)), ("background-2-3.bmp", Bmp(1, 1, 60))), "yard.lvlx");
        Assert.Equal("background2-3.bmp", modern.Prepare(SmbxArtworkKind.SectionBackground, 3)!.Path);
        var local = new SmbxArtworkCatalog(Bundle(("background2-3.bmp", Bmp(1, 1, 30)), ("yard/background-2-3.bmp", Bmp(1, 1, 60))), "yard.lvlx");
        Assert.Equal("yard/background-2-3.bmp", local.Prepare(SmbxArtworkKind.SectionBackground, 3)!.Path);
    }

    [Fact]
    public void LocalImageAndConfigurationOverrideEpisodeWithoutMixingMasks()
    {
        var bundle = Bundle(("episode/yard/NPC-7.BMP", Bmp(2, 4, 24)), ("episode/npc-7.bmp", Bmp(2, 4, 80)),
            ("episode/npc-7m.bmp", Bmp(2, 4, 255)),
            ("episode/npc-7.txt", Text("gfxwidth=2\ngfxheight=2\nframes=1\nframestyle=1\n")),
            ("episode/yard/npc-7.txt", Text("gfxwidth=2\ngfxheight=1\nframes=2\nframestyle=1\nscript=untouched.lua")));
        var catalog = new SmbxArtworkCatalog(bundle, "episode/yard.lvlx");
        var asset = Assert.IsType<SmbxArtwork>(catalog.Prepare(SmbxArtworkKind.Npc, 7));
        Assert.Equal("episode/yard/NPC-7.BMP", asset.Path);
        Assert.Equal((byte)24, asset.Image.Pixels[0]); Assert.Equal((byte)255, asset.Image.Pixels[3]);
        Assert.Equal(2, asset.NpcGraphics.Integer("frames"));
        Assert.True(asset.NpcGraphics.TryGet("script", out var script)); Assert.Equal("untouched.lua", script);
        Assert.Same(asset, catalog.Prepare(SmbxArtworkKind.Npc, 7)); Assert.Equal(32, catalog.DecodedBytes);
        Assert.Null(catalog.Prepare(SmbxArtworkKind.Block, 7));
    }

    [Fact]
    public void AlphaPngWinsWithinDirectoryAndRetainsExactPixels()
    {
        byte[] rgba = [12, 40, 90, 127];
        var catalog = new SmbxArtworkCatalog(Bundle(("block-1.png", Png(rgba, 1, 1)), ("block-1.bmp", Bmp(1, 1, 200))), "yard.lvlx");
        var asset = Assert.IsType<SmbxArtwork>(catalog.Prepare(SmbxArtworkKind.Block, 1));
        Assert.Equal("block-1.png", asset.Path); Assert.Equal(rgba, asset.Image.Pixels.ToArray());
    }

    [Fact]
    public void AmbiguousCaseAndEscapingPathsFailBeforePreparingAssets()
    {
        Assert.Throws<FormatException>(() => new SmbxArtworkCatalog(Bundle(("block-1.bmp", Bmp(1, 1, 0)), ("BLOCK-1.BMP", Bmp(1, 1, 1))), "yard.lvl"));
        Assert.Throws<FormatException>(() => new SmbxArtworkCatalog(Bundle(), "../outside.lvl"));
    }

    [Fact]
    public void BinaryMaskOwnsOutputAndRejectsUnrepresentableLegacyCompositing()
    {
        byte[] rgba = [40, 50, 60, 0, 0, 0, 0, 255];
        var color = RasterImage.FromRgba(2, 1, rgba);
        var mask = RasterImage.FromRgba(2, 1, [0, 0, 0, 255, 255, 255, 255, 255]);
        var result = color.WithBinaryMask(mask, true, true);
        Assert.Equal(new byte[] { 40, 50, 60, 255, 0, 0, 0, 0 }, result.Pixels.ToArray());
        Assert.Equal(rgba, color.Pixels.ToArray()); rgba[0] = 99; Assert.Equal((byte)40, color.Pixels[0]);
        Assert.Throws<FormatException>(() => color.WithBinaryMask(RasterImage.FromRgba(1, 1, [0, 0, 0, 255])));
        Assert.Throws<FormatException>(() => color.WithBinaryMask(RasterImage.FromRgba(2, 1, [0, 0, 0, 255, 128, 128, 128, 255])));
        Assert.Throws<FormatException>(() => RasterImage.FromRgba(2, 1, [40, 50, 60, 255, 1, 0, 0, 255]).WithBinaryMask(mask, true, true));
    }

    [Fact]
    public void MalformedOverrideDoesNotSilentlySelectEpisodeFallbackOrConsumeBudget()
    {
        var catalog = new SmbxArtworkCatalog(Bundle(("yard/block-1.png", Text("malformed")), ("block-1.bmp", Bmp(1, 1, 90))), "yard.lvl");
        var first = Assert.Throws<FormatException>(() => catalog.Prepare(SmbxArtworkKind.Block, 1));
        var second = Assert.Throws<FormatException>(() => catalog.Prepare(SmbxArtworkKind.Block, 1));
        Assert.Equal(first.Message, second.Message); Assert.Equal(0, catalog.DecodedBytes);
    }

    [Fact]
    public void DeclaredHugeImagesFailBeforeDecoderWork()
    {
        byte[] bmp = Bmp(1, 1, 0); BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18), int.MaxValue);
        Assert.Throws<FormatException>(() => RasterImage.DecodeFirstFrame(bmp));
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(22), int.MinValue);
        Assert.Throws<FormatException>(() => RasterImage.DecodeFirstFrame(bmp));
        Assert.Throws<FormatException>(() => RasterImage.ReadDimensions(new byte[AssetBundle.MaximumFileBytes + 1]));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    public void ExplicitVerticalFrameGroupsHaveBoundedIndependentSelection(int style, int groups)
    {
        var pixels = RasterImage.FromRgba(2, 3 * 2 * groups, new byte[2 * 3 * 2 * groups * 4]);
        var config = SmbxNpcGraphics.Read(Text($"gfxwidth=2\ngfxheight=3\nframes=2\nframestyle={style}\ngfxoffsetx=-7\ngfxoffsety=5\nframespeed=16"));
        var sheet = Assert.IsType<SmbxNpcSheet>(config.ResolveSheet(pixels));
        Assert.Equal((0, 3, 2, 3), sheet.Frame(1, false));
        Assert.Equal((0, style == 0 ? 3 : 9, 2, 3), sheet.Frame(1, true));
        if (style == 2) Assert.Equal((0, 21, 2, 3), sheet.Frame(1, true, true));
        else Assert.Throws<ArgumentException>(() => sheet.Frame(0, false, true));
        Assert.Equal(-7, sheet.OffsetX); Assert.Equal(5, sheet.OffsetY);
        Assert.Equal(16, config.Integer("framespeed"));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Frame(2, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.Frame(-1, true));
    }

    [Fact]
    public void MissingBaseLayoutStaysUnresolvedAndUnknownOptionsArePreserved()
    {
        var config = SmbxNpcGraphics.Read(Text("# Original fixture\nframes=2\ncustom-option=\"a=b\""));
        var image = RasterImage.FromRgba(2, 4, new byte[32]);
        Assert.Null(config.ResolveSheet(image));
        var defaults = SmbxNpcGraphics.Read(Text("frames=1\ngfxwidth=2\ngfxheight=2\nframestyle=0"));
        Assert.Equal(2, Assert.IsType<SmbxNpcSheet>(config.ResolveSheet(image, defaults)).Frames);
        Assert.True(config.TryGet("custom-option", out string? value)); Assert.Equal("\"a=b\"", value);
        Assert.Throws<FormatException>(() => SmbxNpcGraphics.Read(Text("frames=2\nFRAMES=3")));
        Assert.Throws<FormatException>(() => SmbxNpcGraphics.Read(Text("frames=1.5")).Integer("frames"));
        Assert.Throws<FormatException>(() => SmbxNpcGraphics.Read([0xff]));
    }

    [Fact]
    public void PackagePreparationDeduplicatesArtAndPreservesUnsupportedSource()
    {
        byte[] source = Text("HEAD\nTL:\"Independent pack yard\";\nHEAD_END\nBLOCK\nID:17;X:0;Y:32;W:32;H:32;\nID:17;X:32;Y:32;W:32;H:32;\nBLOCK_END\nNPC\nID:8000;X:64;Y:0;\nNPC_END\nSCRIPTS\nS:\"retained only\";\nSCRIPTS_END\n");
        var bundle = Bundle(("episode/yard.lvlx", source), ("episode/block-17.bmp", Bmp(2, 2, 90)),
            ("__MACOSX/._yard.lvlx", Text("unused")));
        Assert.Equal(new[] { "episode/yard.lvlx" }, SmbxLevelPack.LevelPaths(bundle));
        var pack = SmbxLevelPack.Open(bundle, "episode/yard.lvlx");
        Assert.Equal(source, pack.Document.WriteOriginal()); Assert.Equal(16, pack.DecodedBytes);
        Assert.True(pack.TryGetArtwork(SmbxArtworkKind.Block, 17, out var image)); Assert.Equal((byte)90, image.Image.Pixels[0]);
        var issue = Assert.Single(pack.Issues.ToArray()); Assert.Equal(SmbxArtworkKind.Npc, issue.Kind); Assert.Equal(8000, issue.Id);
    }

    private static byte[] Text(string text) => Encoding.UTF8.GetBytes(text);
    internal static AssetBundle Bundle(params (string Path, byte[] Bytes)[] files) => AssetBundle.FromFiles(files.Select(f => new KeyValuePair<string, ReadOnlyMemory<byte>>(f.Path, f.Bytes)));
    private static byte[] Png(byte[] pixels, uint width, uint height)
    {
        string path = Path.GetTempFileName();
        try { PngEncoder.SavePng(path, pixels, width, height); return File.ReadAllBytes(path); }
        finally { File.Delete(path); }
    }
    // Original tiny uncompressed BMP fixture; no upstream art, GIF encoding, or engine data.
    internal static byte[] Bmp(int width, int height, byte gray)
    {
        int stride = (width * 3 + 3) & ~3;
        var bytes = new byte[54 + stride * height]; bytes[0] = (byte)'B'; bytes[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2), bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), height);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), 24);
        for (int y = 0; y < height; y++) bytes.AsSpan(54 + y * stride, width * 3).Fill(gray);
        return bytes;
    }
}
