using System.Globalization;
using ProGPU.GameEngine.Assets;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace ProGPU.Samples.Suntrail.Game;

/// <summary>
/// Original readers of the documented Tiled map contracts, plus Suntrail v1/v2.
/// Bounded import parsing; tile decoding/coalescing costs are documented in LevelFiles.Tiles.cs.
/// Files are capped at 1 MiB and compiled levels at 256 objects; no reflection,
/// ambient file access, code execution, or parsing during simulation/rendering.
/// External references resolve only against an explicitly supplied immutable asset bundle.
/// Unsupported geometry fails transactionally instead of changing collision rules.
/// </summary>
public static partial class LevelFiles
{
    private static readonly string[] Kinds = ["ground", "ledge", "moving", "crate", "pipe", "stone", "coin", "relic", "enemy", "hazard", "checkpoint", "spawn", "exit", "saw", "flame", "crusher", "spring", "conveyor", "ice", "crumble", "hopper", "hoverer"];
    public static string KindName(LevelObjectKind kind) => Kinds[(int)kind];
    private static LevelObjectKind ParseKind(string text)
    {
        int index = Array.IndexOf(Kinds, text.ToLowerInvariant());
        return index >= 0 ? (LevelObjectKind)index : throw new FormatException($"Unsupported object class '{text}'. Assign a Suntrail gameplay class before importing.");
    }

    public static int MaximumInputBytes(string fileName) => Path.GetExtension(fileName).Equals(".zip", StringComparison.OrdinalIgnoreCase)
        ? AssetBundle.MaximumArchiveBytes : LevelDocument.MaximumBytes;

    public static LevelDocument Read(ReadOnlyMemory<byte> bytes, string fileName, AssetBundle? resources = null) =>
        ReadCore(bytes, fileName, resources, true);

    private static LevelDocument ReadCore(ReadOnlyMemory<byte> bytes, string fileName, AssetBundle? resources, bool validateConnections)
    {
        if (bytes.Length > MaximumInputBytes(fileName)) throw new FormatException("Maps must be at most 1 MiB and ZIP packages at most 16 MiB.");
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            var document = extension switch
            {
                ".json" or ".tmj" or ".suntrail" => ReadJson(bytes, fileName, resources),
                ".tmx" => ReadTmx(bytes, fileName, resources),
                ".zip" when resources is null => ReadPackage(AssetBundle.FromZip(bytes)),
                ".zip" => throw new FormatException("Nested ZIP packages are not supported."),
                ".nes" => throw new FormatException("NES cartridge level decoding is not available yet. This loader currently accepts Suntrail and Tiled maps."),
                ".lvl" or ".lvlx" => throw new FormatException("SMBX level decoding is not available yet. This loader currently accepts Suntrail and Tiled maps."),
                _ => throw new FormatException("Choose a Suntrail .suntrail, Tiled .json/.tmj/.tmx map, or ZIP package.")
            };
            if (validateConnections) document.ValidateConnections();
            return document;
        }
        catch (Exception e) when (e is JsonException or XmlException or InvalidOperationException or OverflowException or KeyNotFoundException or IOException)
        { throw new FormatException("The level file is malformed: " + e.Message, e); }
    }

    private static LevelDocument ReadJson(ReadOnlyMemory<byte> bytes, string fileName, AssetBundle? resources)
    {
        using var json = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
        var root = json.RootElement;
        var items = new List<LevelObject>();
        if (Text(root, "format") == "suntrail") return ReadNative(root);
        if (Text(root, "type") != "map" || Text(root, "orientation") != "orthogonal" || Flag(root, "infinite"))
            throw new FormatException("Only finite orthogonal Tiled maps are supported.");
        ReadLayers(root.GetProperty("layers"), default, items, 0, new TiledTiles(root, fileName, resources));
        return new(PropertyText(root, "suntrail.name", Path.GetFileNameWithoutExtension(fileName)),
            Integer(PropertyNumber(root, "suntrail.biome")), items.ToArray(), PropertyFlag(root, "suntrail.dungeon"));
    }

    private static LevelDocument ReadNative(JsonElement root)
    {
        int version = Integer(Number(root, "version"));
        if (version is not (1 or 2)) throw new FormatException("Unsupported Suntrail document version.");
        var rooms = new List<LevelDocument>();
        if (root.TryGetProperty("rooms", out var children))
        {
            if (version != 2) throw new FormatException("Connected rooms require Suntrail version 2.");
            foreach (var room in children.EnumerateArray())
            {
                if (rooms.Count == LevelDocument.MaximumRooms - 1) throw new FormatException("A trail supports at most eight rooms.");
                if (room.TryGetProperty("rooms", out _)) throw new FormatException("Rooms cannot contain nested trails.");
                rooms.Add(ReadNativeRoom(room, []));
            }
        }
        var result = ReadNativeRoom(root, rooms.ToArray());
        return result;
    }

    private static LevelDocument ReadNativeRoom(JsonElement room, LevelDocument[] children)
    {
        var items = new List<LevelObject>();
        foreach (var item in room.GetProperty("objects").EnumerateArray()) ReadObject(item, default, true, items);
        return new(Text(room, "name"), Integer(Number(room, "biome")), items.ToArray(), Flag(room, "dungeon"), children);
    }

    private static void ReadLayers(JsonElement layers, System.Numerics.Vector2 offset, List<LevelObject> items, int depth, TiledTiles tiles)
    {
        if (depth > 16) throw new FormatException("Tiled groups may be nested at most 16 levels.");
        foreach (var layer in layers.EnumerateArray())
        {
            var position = offset + new System.Numerics.Vector2(Number(layer, "offsetx"), Number(layer, "offsety"));
            string type = Text(layer, "type");
            if (type == "group") ReadLayers(layer.GetProperty("layers"), position, items, depth + 1, tiles);
            else if (type == "objectgroup")
                foreach (var item in layer.GetProperty("objects").EnumerateArray()) ReadObject(item, position, false, items);
            else if (type == "tilelayer") tiles.Read(layer, position, items);
            else throw new FormatException($"Layer '{Text(layer, "name")}' uses {type}. Image layers are not supported yet.");
        }
    }

    private static void ReadObject(JsonElement item, System.Numerics.Vector2 offset, bool native, List<LevelObject> items)
    {
        if (!native && (Number(item, "rotation") != 0 || Flag(item, "ellipse") || Flag(item, "capsule") || item.TryGetProperty("polygon", out _) ||
            item.TryGetProperty("polyline", out _) || item.TryGetProperty("gid", out _) || item.TryGetProperty("template", out _) || item.TryGetProperty("text", out _)))
            throw new FormatException("Tiled objects must be unrotated rectangles or points without templates or tile images.");
        string kind = native ? Text(item, "kind") : Text(item, "type", Text(item, "class"));
        Add(items, new(ParseKind(kind), new(Number(item, "x") + offset.X, Number(item, "y") + offset.Y,
            Number(item, "width"), Number(item, "height")),
            native ? Number(item, "travel") : PropertyNumber(item, "travel"),
            native ? Number(item, "phase") : PropertyNumber(item, "phase"),
            native ? Number(item, "verticalTravel") : PropertyNumber(item, "verticalTravel"),
            native ? Integer(Number(item, "pipeLink")) : Integer(PropertyNumber(item, "suntrail.pipeLink"))));
    }

    private static LevelDocument ReadTmx(ReadOnlyMemory<byte> bytes, string fileName, AssetBundle? resources)
    {
        using var stream = new MemoryStream(bytes.ToArray(), false);
        using var reader = XmlReader.Create(stream, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = LevelDocument.MaximumBytes, IgnoreComments = true });
        var root = XDocument.Load(reader).Root ?? throw new FormatException("Missing TMX map.");
        if (root.Name != "map" || Attribute(root, "orientation") != "orthogonal" || Attribute(root, "infinite", "0") != "0")
            throw new FormatException("Only finite orthogonal Tiled maps are supported.");
        var items = new List<LevelObject>();
        ReadXmlLayers(root, default, items, 0, new TiledTiles(root, fileName, resources));
        return new(XmlProperty(root, "suntrail.name", Path.GetFileNameWithoutExtension(fileName)), Integer(ParseNumber(XmlProperty(root, "suntrail.biome", "0"))), items.ToArray(), XmlPropertyFlag(root, "suntrail.dungeon"));
    }

    private static void ReadXmlLayers(XElement parent, System.Numerics.Vector2 offset, List<LevelObject> items, int depth, TiledTiles tiles)
    {
        if (depth > 16) throw new FormatException("Tiled groups may be nested at most 16 levels.");
        foreach (var layer in parent.Elements())
        {
            if (layer.Name == "properties" || layer.Name == "tileset") continue;
            var position = offset + new System.Numerics.Vector2(XmlNumber(layer, "offsetx"), XmlNumber(layer, "offsety"));
            if (layer.Name == "group") { ReadXmlLayers(layer, position, items, depth + 1, tiles); continue; }
            if (layer.Name == "layer") { tiles.Read(layer, position, items); continue; }
            if (layer.Name != "objectgroup") throw new FormatException($"TMX layer '{Attribute(layer, "name")}' uses unsupported {layer.Name} geometry.");
            foreach (var item in layer.Elements("object"))
            {
                if (XmlNumber(item, "rotation") != 0 || item.Attribute("gid") is not null || item.Attribute("template") is not null ||
                    item.Elements().Any(e => e.Name != "properties" && e.Name != "point"))
                    throw new FormatException("TMX objects must be unrotated rectangles or points without templates or tile images.");
                Add(items, new(ParseKind(Attribute(item, "type", Attribute(item, "class"))),
                    new(XmlNumber(item, "x") + position.X, XmlNumber(item, "y") + position.Y, XmlNumber(item, "width"), XmlNumber(item, "height")),
                    ParseNumber(XmlProperty(item, "travel", "0")), ParseNumber(XmlProperty(item, "phase", "0")), ParseNumber(XmlProperty(item, "verticalTravel", "0")), Integer(ParseNumber(XmlProperty(item, "suntrail.pipeLink", "0")))));
            }
        }
    }

    private static void Add(List<LevelObject> items, LevelObject item)
    {
        if (items.Count == LevelDocument.MaximumObjects) throw new FormatException("This level contains too many objects.");
        items.Add(item);
    }
    private static int Integer(float value) => value == MathF.Truncate(value) ? checked((int)value) : throw new FormatException("An integer value is required.");
    private static string Text(JsonElement e, string key, string fallback = "") => e.TryGetProperty(key, out var v) ? v.GetString() ?? fallback : fallback;
    private static float Number(JsonElement e, string key) => e.TryGetProperty(key, out var v) ? v.GetSingle() : 0;
    private static bool Flag(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.GetBoolean();
    private static JsonElement Property(JsonElement e, string key)
    {
        if (e.TryGetProperty("properties", out var properties))
            foreach (var p in properties.EnumerateArray()) if (Text(p, "name") == key) return p.GetProperty("value");
        return default;
    }
    private static bool PropertyFlag(JsonElement e, string key) { var p = Property(e, key); return p.ValueKind != JsonValueKind.Undefined && p.GetBoolean(); }
    private static bool XmlPropertyFlag(XElement e, string key) => XmlProperty(e, key, "false") switch
    { "true" or "1" => true, "false" or "0" => false, _ => throw new FormatException($"Property '{key}' must be a boolean.") };
    private static float PropertyNumber(JsonElement e, string key) { var p = Property(e, key); return p.ValueKind == JsonValueKind.Undefined ? 0 : p.GetSingle(); }
    private static string PropertyText(JsonElement e, string key, string fallback) { var p = Property(e, key); return p.ValueKind == JsonValueKind.Undefined ? fallback : p.GetString() ?? fallback; }
    private static string Attribute(XElement e, string key, string fallback = "") => (string?)e.Attribute(key) ?? fallback;
    private static float XmlNumber(XElement e, string key) => ParseNumber(Attribute(e, key, "0"));
    private static float ParseNumber(string text) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : throw new FormatException($"Invalid number '{text}'.");
    private static string XmlProperty(XElement e, string key, string fallback) =>
        e.Element("properties")?.Elements("property").FirstOrDefault(p => Attribute(p, "name") == key) is { } p ? Attribute(p, "value", p.Value) : fallback;

    public static byte[] Write(LevelDocument document)
    {
        document.ValidateConnections();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new() { Indented = true }))
            WriteRoom(writer, document, true);
        if (stream.Length > LevelDocument.MaximumBytes) throw new FormatException("Level files must be at most 1 MiB.");
        return stream.ToArray();
    }

    private static void WriteRoom(Utf8JsonWriter writer, LevelDocument document, bool root)
    {
        writer.WriteStartObject();
        if (root) { writer.WriteString("format", "suntrail"); writer.WriteNumber("version", 2); }
        writer.WriteString("name", document.Name); writer.WriteNumber("biome", document.Biome);
        writer.WriteBoolean("dungeon", document.IsDungeon); writer.WriteStartArray("objects");
        foreach (var item in document.Objects)
        {
            writer.WriteStartObject(); writer.WriteString("kind", KindName(item.Kind));
            writer.WriteNumber("x", item.Bounds.X); writer.WriteNumber("y", item.Bounds.Y);
            writer.WriteNumber("width", item.Bounds.Width); writer.WriteNumber("height", item.Bounds.Height);
            writer.WriteNumber("travel", item.Travel); writer.WriteNumber("phase", item.Phase); writer.WriteNumber("verticalTravel", item.VerticalTravel);
            if (item.PipeLink != 0) writer.WriteNumber("pipeLink", item.PipeLink);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        if (root && document.RoomCount > 1)
        {
            writer.WriteStartArray("rooms");
            for (int i = 1; i < document.RoomCount; i++) WriteRoom(writer, document.GetRoom(i), false);
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }
}
