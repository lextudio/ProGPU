using ProGPU.GameEngine.Assets;
using System.Text.Json;

namespace ProGPU.Samples.Suntrail.Game;

public static partial class LevelFiles
{
    private const string PackageManifest = "suntrail.package.json";

    private static LevelDocument ReadPackage(AssetBundle bundle)
    {
        string entry;
        var roomNames = new List<string>();
        var manifests = bundle.Paths.Where(path => Path.GetFileName(path) == PackageManifest).Take(2).ToArray();
        if (manifests.Length > 1) throw new FormatException("A ZIP package may contain only one suntrail.package.json manifest.");
        string manifestPath = manifests.Length == 1 ? manifests[0] : PackageManifest;
        if (bundle.TryGet(manifestPath, out var manifestBytes))
        {
            if (manifestBytes.Length > LevelDocument.MaximumBytes) throw new FormatException("The package manifest is too large.");
            using var manifest = JsonDocument.Parse(manifestBytes, new() { MaxDepth = 16 });
            var root = manifest.RootElement;
            if (Text(root, "format") != "suntrail-package" || Number(root, "version") != 1)
                throw new FormatException("Unsupported Suntrail package manifest.");
            entry = AssetBundle.ResolvePath(manifestPath, Text(root, "entry"));
            if (root.TryGetProperty("rooms", out var rooms))
                foreach (var room in rooms.EnumerateArray())
                {
                    if (roomNames.Count == LevelDocument.MaximumRooms - 1) throw new FormatException("A trail supports at most eight rooms.");
                    roomNames.Add(AssetBundle.ResolvePath(manifestPath, room.GetString() ?? ""));
                }
        }
        else
        {
            var candidates = new List<string>();
            foreach (string path in bundle.Paths)
            {
                if (path.StartsWith("__MACOSX/", StringComparison.Ordinal) || path.Split('/').Any(part => part.StartsWith('.'))) continue;
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension is ".tmx" or ".tmj" or ".suntrail") candidates.Add(path);
                else if (extension == ".json")
                {
                    var bytes = bundle.Get(path);
                    if (bytes.Length > LevelDocument.MaximumBytes) continue;
                    // JSON tilesets and editor metadata are not candidate maps.
                    if (IsPackageMap(bytes)) candidates.Add(path);
                }
            }
            if (candidates.Count != 1)
                throw new FormatException("A ZIP with several maps needs suntrail.package.json naming its entry map and optional rooms.");
            entry = candidates[0];
        }
        var seen = new HashSet<string>(StringComparer.Ordinal) { entry };
        var first = ReadCore(bundle.Get(entry), entry, bundle, false);
        if (roomNames.Count == 0) { first.ValidateConnections(); return first; }
        if (first.RoomCount != 1) throw new FormatException("Package room lists cannot contain already-nested trails.");
        var children = new LevelDocument[roomNames.Count];
        for (int i = 0; i < children.Length; i++)
        {
            string path = roomNames[i];
            if (!seen.Add(path)) throw new FormatException("A package room may only be listed once.");
            children[i] = ReadCore(bundle.Get(path), path, bundle, false);
            if (children[i].RoomCount != 1) throw new FormatException("Package rooms cannot contain nested trails.");
        }
        var document = new LevelDocument(first.Name, first.Biome, first.Objects, first.IsDungeon, children);
        document.ValidateConnections(); return document;
    }

    private static bool IsPackageMap(ReadOnlyMemory<byte> bytes)
    {
        // Unrelated metadata must not prevent discovery. A selected map or manifest
        // still goes through the strict parser and reports its actual format error.
        try
        {
            using var json = JsonDocument.Parse(bytes, new() { MaxDepth = 32 });
            var root = json.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                (HasString(root, "type", "map") || HasString(root, "format", "suntrail"));
        }
        catch (JsonException) { return false; }
    }

    private static bool HasString(JsonElement root, string property, string expected) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.ValueEquals(expected);
}
