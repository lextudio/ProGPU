using System.Text;
using ProGPU.GameEngine.Assets;
using ProGPU.Samples.Suntrail.Game.Import;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SmbxConfigTests
{
    [Fact]
    public void IniSourcePreservesCommentsUnknownValuesAndOwnedBytes()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\ufeff[main]\r\nname=\"A; B\" ; outside comment\r\nfuture=@Variant(opaque)\r\nvalue=1.25\r\nflag=true\r\n[other]\r\ncount=-7\r\n");
        byte[] expected = (byte[])bytes.Clone(); var document = SmbxConfigDocument.Read(bytes); Array.Fill(bytes, (byte)0);
        Assert.Equal(expected, document.WriteOriginal());
        var main = document.GetSection("MAIN"); Assert.Equal("A; B", main.String("name"));
        Assert.True(main.TryGetRaw("future", out var raw)); Assert.Equal("@Variant(opaque)", raw);
        Assert.Equal(1.25, main.Number("value")); Assert.True(main.Boolean("flag"));
        Assert.Equal(-7, document.GetSection("other").Integer("count")); Assert.Null(main.Integer("missing"));
        Assert.Throws<FormatException>(() => main.Integer("value"));
    }

    [Theory]
    [InlineData("[main]\nvalue=1\nVALUE=2")]
    [InlineData("[main]\n[MAIN]")]
    [InlineData("value=1")]
    [InlineData("[main]\nname=\"unterminated")]
    [InlineData("[main] trailing")]
    public void AmbiguousOrMalformedConfigurationFails(string text) =>
        Assert.Throws<FormatException>(() => SmbxConfigDocument.Read(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void SplitDefinitionsResolveFromTheSelectedPackageRootWithoutInventingRules()
    {
        var files = Files(); var pack = new SmbxConfigPack(AssetBundle.FromFiles(files), "base/main.ini");
        var npc = Assert.IsType<SmbxObjectDefinition>(pack.Get(SmbxArtworkKind.Npc, 17));
        Assert.Equal("base/objects/actors/npc-17.ini", npc.SourcePath);
        Assert.Equal(21, npc.PhysicalWidth); Assert.Equal(37, npc.PhysicalHeight);
        Assert.Equal(64, npc.GraphicsWidth); Assert.Equal(48, npc.GraphicsHeight);
        Assert.Equal("Original test actor", npc.Name); Assert.Equal("fixture.png", npc.ImageName);
        Assert.Equal(6, npc.Fields.Integer("frames")); Assert.True(npc.Fields.TryGetRaw("algorithm", out var algorithm)); Assert.Equal("future-rule", algorithm);
        Assert.Same(npc, pack.Get(SmbxArtworkKind.Npc, 17)); Assert.Null(pack.Get(SmbxArtworkKind.Npc, 18));
        var block = Assert.IsType<SmbxObjectDefinition>(pack.Get(SmbxArtworkKind.Block, 33));
        Assert.Equal(2, block.Fields.Integer("collision")); Assert.Equal(9, block.Fields.Integer("shape-type"));
        Assert.Null(block.PhysicalWidth); Assert.Equal("base/art", pack.GraphicsDirectory);
        Assert.Equal(files.Single(f => f.Key.EndsWith("npc-17.ini")).Value.ToArray(), npc.Source.WriteOriginal());
    }

    [Fact]
    public void EscapingIndexDirectoriesAndAmbientApplicationRootsReject()
    {
        var files = Files(); int index = files.FindIndex(f => f.Key.EndsWith("lvl_npc.ini"));
        files[index] = new(files[index].Key, Encoding.UTF8.GetBytes("[npc-main]\nconfig-dir=../../outside"));
        Assert.Throws<FormatException>(() => new SmbxConfigPack(AssetBundle.FromFiles(files), "base/main.ini"));
        files = Files(); files[0] = new("base/main.ini", Encoding.UTF8.GetBytes("[main]\napplication-dir=1"));
        Assert.Throws<FormatException>(() => new SmbxConfigPack(AssetBundle.FromFiles(files), "base/main.ini"));
    }

    [Fact]
    public void TypedDimensionsRejectInvalidValuesWithoutLosingTheirSource()
    {
        var document = SmbxConfigDocument.Read("[npc]\nphysical-width=-1\ngfx-width=1.5\n"u8);
        var definition = new SmbxObjectDefinition(SmbxArtworkKind.Npc, 17, "npc-17.ini", document, document.GetSection("npc"));
        Assert.Throws<FormatException>(() => definition.PhysicalWidth); Assert.Throws<FormatException>(() => definition.GraphicsWidth);
        Assert.True(definition.Fields.TryGetRaw("physical-width", out var raw)); Assert.Equal("-1", raw);
    }

    private static List<KeyValuePair<string, ReadOnlyMemory<byte>>> Files() => new()
    {
        new("base/main.ini", Encoding.UTF8.GetBytes("[main]\nconfig_name=Original fixture\napplication-dir=0\ngraphics-level=art\n")),
        new("base/lvl_npc.ini", Encoding.UTF8.GetBytes("[npc-main]\ntotal=20\nconfig-dir=objects/actors\n")),
        new("base/lvl_blocks.ini", Encoding.UTF8.GetBytes("[blocks-main]\ntotal=40\nconfig-dir=objects/blocks\n")),
        new("base/objects/actors/npc-17.ini", Encoding.UTF8.GetBytes("[npc]\nname=Original test actor\nimage=fixture.png\nphysical-width=21\nphysical-height=37\ngfx-width=64\ngfx-height=48\nframes=6\nalgorithm=future-rule\n")),
        new("base/objects/blocks/block-33.ini", Encoding.UTF8.GetBytes("[block]\ncollision=2\nshape-type=9\nfuture=preserved\n"))
    };
}
