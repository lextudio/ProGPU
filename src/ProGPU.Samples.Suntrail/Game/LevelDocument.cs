using System.Numerics;

namespace ProGPU.Samples.Suntrail.Game;

public enum LevelObjectKind { Ground, Ledge, Moving, Crate, Pipe, Stone, Coin, Relic, Enemy, Hazard, Checkpoint, Spawn, Exit, Saw, Flame, Crusher, Spring, Conveyor, Ice, Crumble, Hopper, Hoverer }
public readonly record struct LevelObject(LevelObjectKind Kind, Box Bounds, float Travel = 0, float Phase = 0, float VerticalTravel = 0, int PipeLink = 0);

/// <summary>
/// Immutable, validated authoring snapshot, independent of mutable play state.
/// Creation is O(N); gameplay receives its own arrays. Limits bound simulation and
/// worst-case procedural decoration without changing the renderer's quality.
/// </summary>
public sealed class LevelDocument
{
    public const int MaximumObjects = 256;
    public const int MaximumRooms = 8;
    public const int MaximumBytes = 1_048_576;
    private readonly LevelObject[] _objects;
    private readonly LevelDocument[] _rooms;
    public bool IsDungeon { get; }
    public int RoomCount => 1 + _rooms.Length;
    public LevelDocument GetRoom(int index) => index == 0 ? this : _rooms[index - 1];
    public string Name { get; }
    public int Biome { get; }
    public ReadOnlySpan<LevelObject> Objects => _objects;

    public LevelDocument(string name, int biome, ReadOnlySpan<LevelObject> objects, bool isDungeon = false, ReadOnlySpan<LevelDocument> rooms = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl))
            throw new FormatException("A level name must contain 1–80 printable characters.");
        if ((uint)biome >= Level.Names.Length) throw new FormatException("Biome must be from 0 through 7.");
        if (objects.Length > MaximumObjects) throw new FormatException($"A level supports at most {MaximumObjects} objects.");
        if (rooms.Length >= MaximumRooms || rooms.ToArray().Any(r => r is null || r.RoomCount != 1))
            throw new FormatException("A trail supports eight flat rooms; rooms cannot contain nested trails.");
        int spawns = 0, exits = 0, budget = 0;
        foreach (var item in objects)
        {
            var b = item.Bounds;
            if (item.PipeLink < 0 || item.PipeLink > 1024 || (item.PipeLink != 0 && item.Kind != LevelObjectKind.Pipe))
                throw new FormatException("Only pipes may have a connection number, from 1 through 1024.");
            if (item.PipeLink != 0 && (item.Travel != 0 || item.VerticalTravel != 0 || b.Width < GameSession.PlayerWidth || b.Y < GameSession.PlayerHeight))
                throw new FormatException("Connected pipes must be stationary, at least player width, and have room above their entrance.");
            if (item.Kind == LevelObjectKind.Conveyor && item.VerticalTravel != 0)
                throw new FormatException("Conveyors use travel as belt speed and cannot have vertical travel.");
            if (!Enum.IsDefined(item.Kind) || !Finite(b.X, b.Y, b.Width, b.Height, item.Travel, item.Phase, item.VerticalTravel))
                throw new FormatException("Object kinds and coordinates must be valid and finite.");
            if (b.X < 0 || b.Right > 32_000 || b.Y < 0 || b.Y > 950 || b.Width < 0 || b.Width > 2_000 || b.Height < 0 || b.Height > 600 || b.Bottom > 1550)
                throw new FormatException("Objects must fit within x 0–32000 and y 0–950, with width ≤2000 and height ≤600.");
            bool point = item.Kind is LevelObjectKind.Coin or LevelObjectKind.Relic or LevelObjectKind.Checkpoint or LevelObjectKind.Spawn or LevelObjectKind.Exit;
            if (!point && (b.Width < 8 || b.Height < 8)) throw new FormatException("Solid objects must be at least 8 × 8 units.");
            if (item.Kind is LevelObjectKind.Enemy or LevelObjectKind.Hopper or LevelObjectKind.Hoverer && (b.Width != 42 || b.Height != 34))
                throw new FormatException("Enemy profiles use a 42 × 34 collision box.");
            if (Math.Abs(item.Travel) > 500 || Math.Abs(item.VerticalTravel) > 300 || Math.Abs(item.Phase) > 100)
                throw new FormatException("Object motion exceeds the supported range.");
            if (item.Kind == LevelObjectKind.Spawn) spawns++;
            if (item.Kind == LevelObjectKind.Exit) exits++;
            // Ground emits up to three plants per 72 units plus cliff/landmarks;
            // reserve 400 sprites for the background, actor, particles and HUD.
            budget += item.Kind == LevelObjectKind.Ground ? 12 + 3 * (int)(b.Width / 72) : 4;
        }
        if (spawns != 1 || exits != 1) throw new FormatException("A playable level needs exactly one spawn and one exit.");
        if (budget > 1600) throw new FormatException("This map exceeds the procedural artwork budget. Split it into smaller rooms.");
        Name = name; Biome = biome; IsDungeon = isDungeon; _objects = objects.ToArray(); _rooms = rooms.ToArray();
    }

    private static bool Finite(float a, float b, float c, float d, float e, float f, float g) =>
        float.IsFinite(a) && float.IsFinite(b) && float.IsFinite(c) && float.IsFinite(d) && float.IsFinite(e) && float.IsFinite(f) && float.IsFinite(g);

    // Individual room snapshots may contain one half of a connection while being
    // assembled. Saving and starting a trail validate the complete graph.
    public void ValidateConnections()
    {
        var links = new Dictionary<int, List<int>>();
        for (int room = 0; room < RoomCount; room++)
            foreach (var item in GetRoom(room).Objects)
                if (item.PipeLink != 0)
                {
                    if (!links.TryGetValue(item.PipeLink, out var endpoints)) links.Add(item.PipeLink, endpoints = []);
                    endpoints.Add(room);
                }
        foreach (var pair in links)
            if (pair.Value.Count != 2) throw new FormatException($"Pipe connection {pair.Key} needs exactly two endpoints.");
        Span<bool> visited = stackalloc bool[MaximumRooms]; visited.Clear(); visited[0] = true;
        for (int pass = 0; pass < RoomCount; pass++)
            foreach (var endpoints in links.Values)
                if (visited[endpoints[0]] || visited[endpoints[1]]) visited[endpoints[0]] = visited[endpoints[1]] = true;
        for (int room = 1; room < RoomCount; room++)
            if (!visited[room]) throw new FormatException($"Room {room + 1} needs a pipe route from the first room.");
    }

    public Level CreateLevel() => new(this);

    /// <summary>An original editable branching trail: orchard, crystal vault and sky bridges.</summary>
    public static LevelDocument CreateLinkedStarter()
    {
        var vault = new LevelDocument("Crystal sluice", 2,
        [
            new(LevelObjectKind.Spawn, new(150, 456, 0, 0)),
            new(LevelObjectKind.Ground, new(0, 600, 900, 500)),
            new(LevelObjectKind.Ground, new(1060, 600, 740, 500)),
            new(LevelObjectKind.Pipe, new(112, 504, 96, 96), PipeLink: 1),
            new(LevelObjectKind.Moving, new(880, 504, 160, 24), 80),
            new(LevelObjectKind.Crusher, new(530, 300, 64, 80), 160, .4f),
            new(LevelObjectKind.Ledge, new(430, 424, 160, 24)),
            new(LevelObjectKind.Relic, new(500, 380, 0, 0)),
            new(LevelObjectKind.Checkpoint, new(1180, 600, 0, 0)),
            new(LevelObjectKind.Pipe, new(1380, 504, 96, 96), PipeLink: 3),
            new(LevelObjectKind.Exit, new(1650, 600, 0, 0))
        ], true);
        var sky = new LevelDocument("Skybridge crossing", 7,
        [
            new(LevelObjectKind.Spawn, new(150, 456, 0, 0)),
            new(LevelObjectKind.Ground, new(0, 600, 420, 500)),
            new(LevelObjectKind.Pipe, new(112, 504, 96, 96), PipeLink: 2),
            new(LevelObjectKind.Crumble, new(510, 520, 160, 24)),
            new(LevelObjectKind.Moving, new(770, 460, 160, 24), 60, 0, 80),
            new(LevelObjectKind.Crumble, new(1040, 420, 160, 24)),
            new(LevelObjectKind.Coin, new(580, 480, 0, 0)),
            new(LevelObjectKind.Relic, new(1110, 380, 0, 0)),
            new(LevelObjectKind.Ground, new(1300, 600, 680, 500)),
            new(LevelObjectKind.Checkpoint, new(1350, 600, 0, 0)),
            new(LevelObjectKind.Pipe, new(1480, 504, 96, 96), PipeLink: 3),
            new(LevelObjectKind.Exit, new(1810, 600, 0, 0))
        ]);
        return new("Three paths beyond the orchard", 0,
        [
            new(LevelObjectKind.Spawn, new(140, 552, 0, 0)),
            new(LevelObjectKind.Ground, new(0, 600, 900, 500)),
            new(LevelObjectKind.Ground, new(1040, 600, 960, 500)),
            new(LevelObjectKind.Pipe, new(420, 504, 96, 96), PipeLink: 1),
            new(LevelObjectKind.Spring, new(650, 552, 80, 48)),
            new(LevelObjectKind.Ledge, new(730, 360, 150, 24)),
            new(LevelObjectKind.Coin, new(800, 316, 0, 0)),
            new(LevelObjectKind.Enemy, new(1160, 566, 42, 34), 65),
            new(LevelObjectKind.Pipe, new(1410, 504, 96, 96), PipeLink: 2),
            new(LevelObjectKind.Exit, new(1820, 600, 0, 0))
        ], rooms: [vault, sky]);
    }

    public static LevelDocument CreateStarter() => new("My first trail", 0,
    [
        new(LevelObjectKind.Spawn, new(140, 552, 0, 0)),
        new(LevelObjectKind.Ground, new(0, 600, 900, 500)),
        new(LevelObjectKind.Ground, new(1020, 600, 900, 500)),
        new(LevelObjectKind.Ledge, new(420, 504, 150, 24)),
        new(LevelObjectKind.Coin, new(490, 460, 0, 0)),
        new(LevelObjectKind.Checkpoint, new(1150, 600, 0, 0)),
        new(LevelObjectKind.Exit, new(1700, 600, 0, 0))
    ]);
}
