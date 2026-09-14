using System.Buffers.Binary;
using ProGPU.GameEngine.Assets;
using System.Xml;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using System.Xml.Linq;

namespace ProGPU.Samples.Suntrail.Game;

public static partial class LevelFiles
{
    /// <summary>
    /// Original tile-layer importer using Tiled's public GID, encoding and class contracts.
    /// Expected O(B + C + (S + T)*log(S + 1) + N) import time for B encoded bytes, C cells,
    /// S tilesets, T tile definitions and N output objects. Dictionary collision worst case is O(C*T + C*W), W row width.
    /// O(B + C + S + T + W + N) temporary storage, bounded to 65536 total cells, 4096 tilesets/definitions
    /// and 256 output objects. Decompression reads exactly 4*C bytes plus one overflow probe.
    /// Row runs extend only matching rectangles; no scanning all earlier rows or per-frame parsing.
    /// External TSX/TSJ definitions are decoded once per unique package path per map; their total bytes are package-bounded.
    /// Rendering keeps the existing procedural art; referenced tileset images are never loaded.
    /// </summary>
    private sealed class TiledTiles
    {
        private const int MaximumCells = 65_536, MaximumDefinitions = 4_096;
        private readonly JsonElement _json;
        private readonly XElement? _xml;
        private readonly string _fileName;
        private readonly AssetBundle? _resources;
        private readonly Dictionary<string, TileDefinition[]> _external = new(StringComparer.Ordinal);
        private readonly record struct TileDefinition(uint Id, LevelObject Object);
        private Dictionary<uint, LevelObject>? _definitions;
        private int _remainingCells = MaximumCells;
        private int _tileWidth, _tileHeight;
        public TiledTiles(JsonElement root, string fileName, AssetBundle? resources) { _json = root; _fileName = fileName; _resources = resources; }
        public TiledTiles(XElement root, string fileName, AssetBundle? resources) { _xml = root; _fileName = fileName; _resources = resources; }

        private void Initialize()
        {
            if (_definitions is not null) return;
            _tileWidth = Integer(_xml is null ? Number(_json, "tilewidth") : XmlNumber(_xml, "tilewidth"));
            _tileHeight = Integer(_xml is null ? Number(_json, "tileheight") : XmlNumber(_xml, "tileheight"));
            if (_tileWidth is < 8 or > 256 || _tileHeight is < 8 or > 256)
                throw new FormatException("Tile dimensions must be 8–256 logical units.");
            _definitions = [];
            var ranges = new SortedSet<uint>();
            var owners = new Dictionary<uint, uint>();
            void Define(uint first, TileDefinition definition)
            {
                if (_definitions.Count == MaximumDefinitions) throw new FormatException("A map supports at most 4096 gameplay tile definitions.");
                uint gid = checked(first + definition.Id);
                if (gid > 0x0fff_ffff || !_definitions.TryAdd(gid, definition.Object))
                    throw new FormatException("Tileset IDs overlap or exceed the supported range.");
                owners.Add(gid, first);
            }
            void Range(uint first)
            {
                if (ranges.Count == MaximumDefinitions) throw new FormatException("A map supports at most 4096 tilesets.");
                if (first == 0 || first > 0x0fff_ffff || !ranges.Add(first)) throw new FormatException("Tilesets require unique, positive firstgid values.");
            }
            if (_xml is null)
            {
                if (_json.TryGetProperty("tilesets", out var sets))
                    foreach (var set in sets.EnumerateArray())
                    {
                        uint first = set.GetProperty("firstgid").GetUInt32(); Range(first);
                        var definitions = set.TryGetProperty("source", out var source) ? External(source.GetString() ?? "") : JsonDefinitions(set);
                        foreach (var tile in definitions) Define(first, tile);
                    }
            }
            else foreach (var set in _xml.Elements("tileset"))
            {
                uint first = Unsigned(Attribute(set, "firstgid")); Range(first);
                var definitions = set.Attribute("source") is { } source ? External(source.Value) : XmlDefinitions(set);
                foreach (var tile in definitions) Define(first, tile);
            }
            uint[] starts = ranges.ToArray();
            foreach (var pair in owners)
            {
                int index = Array.BinarySearch(starts, pair.Value);
                if (index + 1 < starts.Length && pair.Key >= starts[index + 1])
                    throw new FormatException("A local tile ID extends into the next tileset's GID range.");
            }
        }

        private TileDefinition[] External(string reference)
        {
            if (_resources is null) throw new FormatException($"Tileset '{reference}' is external. Open a ZIP containing the map and its tilesets.");
            string path = AssetBundle.ResolvePath(_fileName, reference);
            if (_external.TryGetValue(path, out var cached)) return cached;
            var bytes = _resources.Get(path);
            if (bytes.Length > LevelDocument.MaximumBytes) throw new FormatException("Tileset definition files must be at most 1 MiB.");
            TileDefinition[] definitions;
            if (Path.GetExtension(path).Equals(".tsx", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = new MemoryStream(bytes.ToArray(), false);
                using var reader = XmlReader.Create(stream, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                    MaxCharactersInDocument = LevelDocument.MaximumBytes, IgnoreComments = true });
                var root = XDocument.Load(reader).Root;
                if (root is null || root.Name != "tileset" || root.Attribute("source") is not null || root.Attribute("firstgid") is not null)
                    throw new FormatException("External TSX files need a tileset root without source or firstgid.");
                definitions = XmlDefinitions(root);
            }
            else if (Path.GetExtension(path).ToLowerInvariant() is ".tsj" or ".json")
            {
                using var json = JsonDocument.Parse(bytes, new() { MaxDepth = 32 }); var root = json.RootElement;
                if (Text(root, "type", "tileset") != "tileset" || root.TryGetProperty("source", out _) || root.TryGetProperty("firstgid", out _))
                    throw new FormatException("External JSON tilesets cannot contain source or firstgid.");
                definitions = JsonDefinitions(root);
            }
            else throw new FormatException("External tilesets must use .tsx, .tsj or .json.");
            _external.Add(path, definitions); return definitions;
        }

        private static TileDefinition[] JsonDefinitions(JsonElement set)
        {
            var result = new List<TileDefinition>();
            if (set.TryGetProperty("tiles", out var tiles))
                foreach (var tile in tiles.EnumerateArray())
                {
                    string kind = Text(tile, "type", Text(tile, "class"));
                    if (kind.Length == 0) continue;
                    if (result.Count == MaximumDefinitions) throw new FormatException("Too many tileset gameplay definitions.");
                    if (tile.TryGetProperty("objectgroup", out _)) throw new FormatException("Tile collision object groups are not supported; use a whole-cell gameplay class.");
                    result.Add(new(tile.GetProperty("id").GetUInt32(), new(ParseKind(kind), default,
                        PropertyNumber(tile, "travel"), PropertyNumber(tile, "phase"), PropertyNumber(tile, "verticalTravel"))));
                }
            return result.ToArray();
        }
        private static TileDefinition[] XmlDefinitions(XElement set)
        {
            var result = new List<TileDefinition>();
            foreach (var tile in set.Elements("tile"))
            {
                string kind = Attribute(tile, "type", Attribute(tile, "class"));
                if (kind.Length == 0) continue;
                if (result.Count == MaximumDefinitions) throw new FormatException("Too many tileset gameplay definitions.");
                if (tile.Element("objectgroup") is not null) throw new FormatException("Tile collision object groups are not supported; use a whole-cell gameplay class.");
                result.Add(new(Unsigned(Attribute(tile, "id")), new(ParseKind(kind), default, ParseNumber(XmlProperty(tile, "travel", "0")),
                    ParseNumber(XmlProperty(tile, "phase", "0")), ParseNumber(XmlProperty(tile, "verticalTravel", "0")))));
            }
            return result.ToArray();
        }

        private int Count(int width, int height)
        {
            Initialize();
            if (width <= 0 || height <= 0 || width > 4000 || height > 194 || (long)width * height > _remainingCells)
                throw new FormatException("Tile layers must be finite and contain at most 65536 cells in total.");
            _remainingCells -= width * height;
            return width * height;
        }
        public void Read(JsonElement layer, Vector2 offset, List<LevelObject> output)
        {
            int width = Integer(Number(layer, "width")), height = Integer(Number(layer, "height"));
            int count = Count(width, height);
            if (layer.TryGetProperty("chunks", out _) || Number(layer, "x") != 0 || Number(layer, "y") != 0)
                throw new FormatException("Chunked tile maps and legacy tile-coordinate layer offsets are not supported.");
            var data = layer.GetProperty("data");
            uint[] cells;
            if (data.ValueKind == JsonValueKind.Array)
            {
                if (Text(layer, "compression").Length != 0 || Text(layer, "encoding", "csv") != "csv")
                    throw new FormatException("JSON tile arrays cannot have binary encoding or compression.");
                if (data.GetArrayLength() != count) throw new FormatException("Tile data length does not match layer dimensions.");
                cells = new uint[count]; int i = 0;
                foreach (var cell in data.EnumerateArray()) cells[i++] = cell.GetUInt32();
            }
            else
            {
                if (Text(layer, "encoding") != "base64") throw new FormatException("JSON string tile data must use base64 encoding.");
                cells = Decode(Text(layer, "data"), Text(layer, "compression"), count);
            }
            Compile(cells, width, height, offset, output);
        }
        public void Read(XElement layer, Vector2 offset, List<LevelObject> output)
        {
            int width = Integer(XmlNumber(layer, "width")), height = Integer(XmlNumber(layer, "height"));
            int count = Count(width, height);
            if (XmlNumber(layer, "x") != 0 || XmlNumber(layer, "y") != 0)
                throw new FormatException("Legacy tile-coordinate layer offsets are not supported.");
            var data = layer.Element("data") ?? throw new FormatException("Missing TMX tile data.");
            if (data.Element("chunk") is not null) throw new FormatException("Chunked tile maps are not supported yet.");
            string encoding = Attribute(data, "encoding"), compression = Attribute(data, "compression");
            uint[] cells;
            if (encoding == "base64") cells = Decode(data.Value, compression, count);
            else
            {
                if (compression.Length != 0) throw new FormatException("Compression requires base64 tile data.");
                cells = new uint[count]; int i = 0;
                void Push(uint gid) { if (i == count) throw new FormatException("Too many tile IDs."); cells[i++] = gid; }
                if (encoding == "csv")
                {
                    // Scan spans rather than allocating a string for every cell.
                    ReadOnlySpan<char> remaining = data.Value.AsSpan();
                    while (true)
                    {
                        int comma = remaining.IndexOf(',');
                        Push(Unsigned(comma < 0 ? remaining : remaining[..comma]));
                        if (comma < 0) break;
                        remaining = remaining[(comma + 1)..];
                    }
                }
                else if (encoding.Length == 0)
                    foreach (var cell in data.Elements("tile")) Push(Unsigned(Attribute(cell, "gid")));
                else throw new FormatException($"Unsupported tile encoding '{encoding}'.");
                if (i != count) throw new FormatException("Tile data length does not match layer dimensions.");
            }
            Compile(cells, width, height, offset, output);
        }
        private static uint Unsigned(ReadOnlySpan<char> text) => uint.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out uint result)
            ? result : throw new FormatException("Tile IDs must be unsigned 32-bit integers.");
        private static uint[] Decode(string encoded, string compression, int count)
        {
            byte[] packed = Convert.FromBase64String(encoded);
            byte[] raw;
            if (compression.Length == 0) raw = packed;
            else
            {
                using var source = new MemoryStream(packed, false);
                using Stream inflater = compression switch
                {
                    "gzip" => new GZipStream(source, CompressionMode.Decompress),
                    "zlib" => new ZLibStream(source, CompressionMode.Decompress),
                    _ => throw new FormatException($"Unsupported tile compression '{compression}'. Use gzip, zlib or uncompressed data.")
                };
                raw = new byte[checked(count * 4)];
                inflater.ReadExactly(raw);
                if (inflater.ReadByte() != -1) throw new FormatException("Decompressed tile data exceeds layer dimensions.");
            }
            if (raw.Length != count * 4) throw new FormatException("Tile data length does not match layer dimensions.");
            var result = new uint[count];
            for (int i = 0; i < count; i++) result[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(i * 4, 4));
            return result;
        }
        private LevelObject? Tile(uint encoded)
        {
            uint gid = encoded & 0x0fff_ffff;
            if (gid == 0) return null;
            if (!_definitions!.TryGetValue(gid, out var tile))
                throw new FormatException($"Tile GID {gid} has no gameplay class. Include referenced tilesets in a ZIP and assign Suntrail classes to used tiles.");
            // Whole-cell static solids are axis-aligned and symmetric under tile flips.
            // Directional actors/markers retain explicit transform diagnostics until their artwork importer exists.
            if ((encoded & 0xe000_0000) != 0 && !Mergeable(tile))
                throw new FormatException("Flipped actor, marker or mechanism tiles are not supported yet.");
            return tile;
        }
        private static bool Mergeable(LevelObject tile) => tile.Kind is LevelObjectKind.Ground or LevelObjectKind.Ledge or LevelObjectKind.Stone
            && tile.Travel == 0 && tile.Phase == 0 && tile.VerticalTravel == 0;
        private readonly record struct Run(LevelObject Tile, int Column, int Width);
        private void Compile(uint[] cells, int width, int height, Vector2 offset, List<LevelObject> output)
        {
            var previous = new Dictionary<Run, LevelObject>();
            var next = new Dictionary<Run, LevelObject>();
            var previousOrder = new List<Run>();
            var nextOrder = new List<Run>();
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width;)
                {
                    var value = Tile(cells[row * width + column]);
                    if (value is not { } tile) { column++; continue; }
                    float x = offset.X + column * _tileWidth, y = offset.Y + row * _tileHeight;
                    if (!Mergeable(tile))
                    {
                        var bounds = tile.Kind switch
                        {
                            LevelObjectKind.Coin or LevelObjectKind.Relic => new Box(x + _tileWidth / 2f, y + _tileHeight / 2f, 0, 0),
                            LevelObjectKind.Spawn => new Box(x + (_tileWidth - GameSession.PlayerWidth) / 2, y + _tileHeight - GameSession.PlayerHeight, 0, 0),
                            LevelObjectKind.Exit or LevelObjectKind.Checkpoint => new Box(x + _tileWidth / 2f, y + _tileHeight, 0, 0),
                            LevelObjectKind.Enemy or LevelObjectKind.Hopper or LevelObjectKind.Hoverer => new Box(x + (_tileWidth - 42) / 2f, y + _tileHeight - 34, 42, 34),
                            _ => new Box(x, y, _tileWidth, _tileHeight)
                        };
                        Add(output, tile with { Bounds = bounds }); column++; continue;
                    }
                    int length = 1, maximum = 2000 / _tileWidth;
                    while (column + length < width && length < maximum && Tile(cells[row * width + column + length]) == tile) length++;
                    var key = new Run(tile, column, length);
                    var rectangle = tile with { Bounds = new(x, y, length * _tileWidth, _tileHeight) };
                    if (previous.Remove(key, out var above))
                    {
                        if (above.Bounds.Height + _tileHeight <= 600) rectangle = above with { Bounds = above.Bounds with { Height = above.Bounds.Height + _tileHeight } };
                        else Add(output, above);
                    }
                    next.Add(key, rectangle); nextOrder.Add(key); column += length;
                }
                foreach (var key in previousOrder)
                    if (previous.TryGetValue(key, out var ended)) Add(output, ended);
                (previous, next) = (next, previous); next.Clear();
                (previousOrder, nextOrder) = (nextOrder, previousOrder); nextOrder.Clear();
            }
            foreach (var key in previousOrder) Add(output, previous[key]);
        }
    }
}
