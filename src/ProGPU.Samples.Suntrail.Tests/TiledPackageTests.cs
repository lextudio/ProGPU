using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ProGPU.GameEngine.Assets;
using ProGPU.Samples.Suntrail.Game;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class TiledPackageTests
{
    // Independent fixtures authored from Tiled's documented source/firstgid contracts.
    private const string Definitions = """
        {"type":"tileset","name":"Gameplay","tilewidth":32,"tileheight":32,
         "tiles":[{"id":0,"type":"ground"},{"id":1,"type":"spawn"},{"id":2,"class":"exit"}]}
        """;
    private const string XmlDefinitions = """
        <tileset name="Gameplay" tilewidth="32" tileheight="32">
          <tile id="0" type="ground"/><tile id="1" type="spawn"/><tile id="2" class="exit"/>
        </tileset>
        """;
    private const string Map = """
        {"type":"map","orientation":"orthogonal","tilewidth":32,"tileheight":32,
         "tilesets":[{"firstgid":17,"source":"../tiles/game.tsj"}],
         "layers":[{"type":"tilelayer","width":10,"height":2,"offsety":400,
           "data":[0,18,0,0,0,0,0,0,19,0,17,17,17,17,17,17,17,17,17,17]}]}
        """;
    private const string XmlMap = """
        <map orientation="orthogonal" tilewidth="32" tileheight="32">
          <tileset firstgid="17" source="../tiles/game.tsx"/>
          <layer width="10" height="2" offsety="400"><data encoding="csv">
            0,18,0,0,0,0,0,0,19,0,17,17,17,17,17,17,17,17,17,17
          </data></layer>
        </map>
        """;

    [Fact]
    public void ExternalJsonAndXmlTilesetsProduceTheSamePlayableGeometry()
    {
        var json = LevelFiles.Read(Zip(("maps/main.tmj", Map), ("tiles/game.tsj", Definitions)), "maps.zip");
        var xml = LevelFiles.Read(Zip(("maps/main.tmx", XmlMap), ("tiles/game.tsx", XmlDefinitions)), "maps.zip");
        Assert.Equal(LevelFiles.Write(json), LevelFiles.Write(xml));
        Assert.Equal(new Box(0, 432, 320, 32), Assert.Single(json.Objects.ToArray(), o => o.Kind == LevelObjectKind.Ground).Bounds);
        var game = new GameSession(); game.StartDocument(json);
        for (int i = 0; i < 600 && game.Mode == GameMode.Playing; i++) game.Step(RoutePilot.GetInput(game));
        Assert.Equal(GameMode.Complete, game.Mode);
    }

    [Fact]
    public void CallerProvidedBundleSupportsReferencesWithoutAmbientFilesystemAccess()
    {
        byte[] definitionBytes = Encoding.UTF8.GetBytes(Definitions);
        var bundle = AssetBundle.FromFiles([new("tiles/game.tsj", definitionBytes)]);
        Array.Fill(definitionBytes, (byte)0);
        var parsed = LevelFiles.Read(Encoding.UTF8.GetBytes(Map), "maps/main.tmj", bundle);
        Assert.Equal(3, parsed.Objects.Length);
        Assert.Throws<FormatException>(() => LevelFiles.Read(Encoding.UTF8.GetBytes(Map), "main.tmj"));
    }

    [Theory]
    [InlineData("../../outside.tsj")]
    [InlineData("/outside.tsj")]
    [InlineData("https://example.com/tiles.tsj")]
    [InlineData("C:\\tiles.tsj")]
    [InlineData("../tiles/missing.tsj")]
    public void InvalidOrMissingReferencesRejectThePackage(string source)
    {
        string escaped = System.Text.Json.JsonSerializer.Serialize(source);
        string map = Map.Replace("\"../tiles/game.tsj\"", escaped);
        Assert.Throws<FormatException>(() => LevelFiles.Read(Zip(("maps/main.tmj", map), ("tiles/game.tsj", Definitions)), "map.zip"));
    }

    [Fact]
    public void PackageManifestBuildsConnectedRoomsAndSaveRetainsTheirRoutes()
    {
        const string manifest = """
            {"format":"suntrail-package","version":1,"entry":"maps/entry.tmj","rooms":["rooms/vault.tmj"]}
            """;
        static string Room(bool dungeon) => """
            {"type":"map","orientation":"orthogonal","properties":[{"name":"suntrail.dungeon","value":DUNGEON}],
             "layers":[{"type":"objectgroup","objects":[
               {"type":"spawn","x":140,"y":552},
               {"type":"ground","x":0,"y":600,"width":900,"height":500},
               {"type":"pipe","x":400,"y":504,"width":96,"height":96,"properties":[{"name":"suntrail.pipeLink","value":9}]},
               {"type":"exit","x":800,"y":600}]}]}
            """.Replace("DUNGEON", dungeon ? "true" : "false");
        var document = LevelFiles.Read(Zip(("suntrail.package.json", manifest), ("maps/entry.tmj", Room(false)), ("rooms/vault.tmj", Room(true))), "rooms.zip");
        Assert.Equal(2, document.RoomCount); Assert.True(document.GetRoom(1).IsDungeon);
        Assert.Equal(9, Assert.Single(document.Objects.ToArray(), o => o.Kind == LevelObjectKind.Pipe).PipeLink);
        var saved = LevelFiles.Write(document); Assert.Equal(saved, LevelFiles.Write(LevelFiles.Read(saved, "rooms.suntrail")));
        var nested = LevelFiles.Read(Zip(("pack/suntrail.package.json", manifest), ("pack/maps/entry.tmj", Room(false)), ("pack/rooms/vault.tmj", Room(true))), "nested.zip");
        Assert.Equal(saved, LevelFiles.Write(nested));
        var game = new GameSession(); game.StartDocument(document); Assert.Single(game.Level.Pipes);
    }

    [Fact]
    public void FinderMetadataIsNotMistakenForAnotherMap()
    {
        var zip = Zip(("maps/main.tmj", Map), ("tiles/game.tsj", Definitions), ("__MACOSX/maps/._main.tmj", "metadata"));
        Assert.Equal(3, LevelFiles.Read(zip, "finder.zip").Objects.Length);
    }

    [Fact]
    public void UnrelatedJsonMetadataDoesNotPreventMapDiscovery()
    {
        var zip = Zip(("maps/main.tmj", Map), ("tiles/game.tsj", Definitions),
            ("editor.json", "{incomplete"), ("settings.json", "{\"type\":32}"));
        Assert.Equal(3, LevelFiles.Read(zip, "metadata.zip").Objects.Length);
    }

    [Fact]
    public void AmbiguousMapsAndDuplicateCanonicalPathsFailExplicitly()
    {
        Assert.Throws<FormatException>(() => LevelFiles.Read(Zip(("maps/a.tmj", Map), ("maps/b.tmj", Map)), "multiple.zip"));
        Assert.Throws<FormatException>(() => AssetBundle.FromZip(Zip(("a.txt", "a"), ("folder/../a.txt", "b"))));
        Assert.Throws<FormatException>(() => AssetBundle.FromZip(Zip(("../a.txt", "a"))));
    }

    [Fact]
    public void DirectoryCountAndChecksumCorruptionAreRejectedBeforeImport()
    {
        var zip = Zip(("map.tmj", "123456789"));
        var corruptCount = (byte[])zip.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(corruptCount.AsSpan(corruptCount.Length - 12), 129);
        Assert.Throws<FormatException>(() => AssetBundle.FromZip(corruptCount));
        // Use stored content, locate its literal independent fixture, and change one byte.
        var broken = (byte[])zip.Clone();
        int offset = broken.AsSpan().IndexOf("123456789"u8); Assert.True(offset >= 0); broken[offset] ^= 1;
        Assert.Throws<FormatException>(() => AssetBundle.FromZip(broken));
    }

    private static byte[] Zip(params (string Name, string Text)[] entries)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var entry in entries)
            {
                using var stream = archive.CreateEntry(entry.Name, CompressionLevel.NoCompression).Open();
                stream.Write(Encoding.UTF8.GetBytes(entry.Text));
            }
        return output.ToArray();
    }
}
