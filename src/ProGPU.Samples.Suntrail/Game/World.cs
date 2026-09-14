using System.Numerics;
using System.Collections.Immutable;
using ProGPU.GameEngine.Simulation;

namespace ProGPU.Samples.Suntrail.Game;

public enum GameMode { Title, Playing, Paused, Fallen, LevelComplete, Complete }
public enum PlatformKind { Ground, Ledge, Moving, Crate, Pipe, Stone, Spring, Conveyor, Ice, Crumble }
public readonly record struct Box(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public bool Intersects(Box b) => X < b.Right && Right > b.X && Y < b.Bottom && Bottom > b.Y;
}
public readonly record struct Platform(Box Bounds, PlatformKind Kind, float Travel = 0, float Phase = 0, float VerticalTravel = 0)
{
    public bool IsOneWay => Kind is PlatformKind.Ledge or PlatformKind.Moving or PlatformKind.Crumble;
    public ContactSurface Contact => Kind switch
    {
        PlatformKind.Ice => new(460, 160),
        PlatformKind.Conveyor => new(2300, 2300, Travel == 0 ? 110 : Travel),
        PlatformKind.Spring => new(2300, 2300, launchSpeed: 960),
        _ => new(2300, 2300)
    };
    public Box At(float time) => Kind == PlatformKind.Conveyor ? Bounds : Bounds with { X = Bounds.X + MathF.Sin(time * 1.4f + Phase) * Travel, Y = Bounds.Y + MathF.Sin(time * 1.4f + Phase) * VerticalTravel };
}
public readonly record struct Checkpoint(float X, float Y);
public enum MechanismKind { Saw, FlameJet, Crusher }
public readonly record struct Mechanism(Box Bounds, MechanismKind Kind, float Phase = 0, float Travel = 0)
{
    public float Cycle(float time) => (time / 3.2f + Phase) - MathF.Floor(time / 3.2f + Phase);
    public bool IsDangerous(float time) => Kind != MechanismKind.FlameJet || Cycle(time) is > .30f and < .64f;
    public Box At(float time) => Kind switch
    {
        MechanismKind.Saw => Bounds with { X = Bounds.X + MathF.Sin(time * 1.8f + Phase) * Travel },
        // Slow retraction, brief held warning, rapid drop, then a grounded pause.
        MechanismKind.Crusher => Bounds with { Y = Bounds.Y + Travel * Drop(Cycle(time)) },
        _ => Bounds
    };
    private static float Drop(float t) => t < .40f ? 1 - t / .40f : t < .65f ? 0 : t < .75f ? (t - .65f) / .10f : 1;
}
public struct Pickup { public Vector2 Position; public bool Collected; public bool IsRelic; }
public enum EnemyKind { Walker, Hopper, Hoverer }
public struct Enemy
{
    public Vector2 Position;
    public float Left, Right, Speed, AnchorY, Phase;
    public EnemyKind Kind;
    public bool Defeated;
    // Original deterministic patrol profiles. Horizontal bounds remain authored;
    // hoppers rest between arcs and hoverers expose a changing stomp window.
    public readonly float VerticalOffset(float time)
    {
        if (Kind == EnemyKind.Hoverer) return -100 - MathF.Sin(time * 2.4f + Phase) * 34;
        if (Kind != EnemyKind.Hopper) return 0;
        float cycle = time / 2.1f + Phase; cycle -= MathF.Floor(cycle);
        float t = Math.Min(1, cycle / .55f);
        return -384 * t * (1 - t);
    }
}
public struct Particle { public Vector2 Position, Velocity; public float Life, MaxLife; public int Kind; }

/// <summary>Original authored platform grammar. Generation is bounded and seeded; no runtime asset discovery.</summary>
public sealed partial class Level
{
    public static readonly string[] Names = ["The waking orchard", "The amber aqueduct", "Crystal cathedral", "The drowned kingdom", "Copperleaf ascent", "The silent glacier", "Furnace of stars", "The last sunrise"];
    public static readonly string[] Regions = ["VERDANT ISLES", "SANDSTONE REACH", "LUMEN CAVERNS", "TIDAL KINGDOM", "AUTUMN HIGHLANDS", "FROSTBOUND PEAKS", "OBSIDIAN FORGE", "CELESTIAL GARDENS"];
    public static readonly string[] VaultNames = ["Rootwater hollow", "The buried sluice", "Prism galleries", "The tidal cistern",
        "The copperwood crypt", "Blueglass tunnels", "The cooling chambers", "The suspended sanctuary"];
    public static readonly string[] Descriptions = [
        "Follow orchard boughs, creek ferries and spring-fed canopy paths.",
        "Cross broken arches and ride the machinery of an ancient aqueduct.",
        "Climb crystal stairs and fragile galleries between staggered flame gates.",
        "Choose between tidal piers, twin ferries and the buried cistern.",
        "Spring into copperleaf canopies above thorny low paths.",
        "Brake on blue ice, catch the lifts and cross fractured snow bridges.",
        "Thread staggered flame gates and pressing conveyors above an ember sea.",
        "Commit to fragile sky relays and drifting sanctuary ferries."];
    public int Index { get; }
    public LevelDocument? Document { get; }
    public string Name => Document?.Name ?? (IsDungeon ? VaultNames[Index] : Names[Index]);
    public bool IsDungeon { get; }
    public int Biome => Index;
    public Box[] Pipes { get; }
    public int[] PipeLinks { get; } = [];
    public Mechanism[] Mechanisms { get; }
    public ImmutableArray<Platform> Platforms { get; private set; }
    private readonly TimedSupportState[] _supportStates;
    private static readonly TimedSupportPolicy CrumbleCycle = new(66, 300);
    public bool IsPlatformSolid(int index, long tick) => Platforms[index].Kind != PlatformKind.Crumble || _supportStates[index].IsSolid(tick, CrumbleCycle);
    public float PlatformWarning(int index, long tick) => Platforms[index].Kind == PlatformKind.Crumble ? _supportStates[index].WarningProgress(tick, CrumbleCycle) : 0;
    public void TouchPlatform(int index, long tick)
    {
        if (Platforms[index].Kind == PlatformKind.Crumble) _supportStates[index].Touch(tick, CrumbleCycle);
    }
    public void ResetSupports() => Array.Clear(_supportStates);
    public Pickup[] Pickups { get; }
    public Enemy[] Enemies { get; }
    public Box[] Hazards { get; }
    public Checkpoint[] Checkpoints { get; }
    public Vector2 Spawn { get; } = new(140, 530);
    public Vector2 Exit { get; }
    private readonly float _customWidth;
    public float Width => Math.Max(Math.Max(Exit.X + 500, _customWidth), _replacementWidth);
    public int CoinCount { get; }

    internal Level(LevelDocument document)
    {
        Document = document; Index = document.Biome; IsDungeon = document.IsDungeon;
        var platforms = new List<Platform>();
        var pickups = new List<Pickup>();
        var enemies = new List<Enemy>();
        var hazards = new List<Box>();
        var checkpoints = new List<Checkpoint>();
        var mechanisms = new List<Mechanism>();
        foreach (var item in document.Objects)
        {
            var b = item.Bounds;
            _customWidth = Math.Max(_customWidth, b.Right + 100);
            var position = new Vector2(b.X, b.Y);
            switch (item.Kind)
            {
                case LevelObjectKind.Spawn: Spawn = position; break;
                case LevelObjectKind.Exit: Exit = position; break;
                case LevelObjectKind.Coin: case LevelObjectKind.Relic:
                    pickups.Add(new() { Position = position, IsRelic = item.Kind == LevelObjectKind.Relic }); break;
                case LevelObjectKind.Checkpoint: checkpoints.Add(new(b.X, b.Y)); break;
                case LevelObjectKind.Enemy: case LevelObjectKind.Hopper: case LevelObjectKind.Hoverer:
                    var enemy = new Enemy { Position = position, Left = b.X, Right = b.X + Math.Abs(item.Travel), Speed = -60,
                        AnchorY = b.Y, Phase = item.Phase, Kind = item.Kind == LevelObjectKind.Hopper ? EnemyKind.Hopper :
                            item.Kind == LevelObjectKind.Hoverer ? EnemyKind.Hoverer : EnemyKind.Walker };
                    enemy.Position.Y += enemy.VerticalOffset(0); enemies.Add(enemy); break;
                case LevelObjectKind.Hazard: hazards.Add(b); break;
                case LevelObjectKind.Saw: case LevelObjectKind.Flame: case LevelObjectKind.Crusher:
                    mechanisms.Add(new(b, item.Kind == LevelObjectKind.Saw ? MechanismKind.Saw : item.Kind == LevelObjectKind.Flame ? MechanismKind.FlameJet : MechanismKind.Crusher, item.Phase, item.Travel)); break;
                default:
                    platforms.Add(new(b, item.Kind switch {
                        LevelObjectKind.Ground => PlatformKind.Ground, LevelObjectKind.Ledge => PlatformKind.Ledge,
                        LevelObjectKind.Moving => PlatformKind.Moving, LevelObjectKind.Crate => PlatformKind.Crate,
                        LevelObjectKind.Pipe => PlatformKind.Pipe, LevelObjectKind.Spring => PlatformKind.Spring,
                        LevelObjectKind.Conveyor => PlatformKind.Conveyor, LevelObjectKind.Ice => PlatformKind.Ice,
                        LevelObjectKind.Crumble => PlatformKind.Crumble, _ => PlatformKind.Stone }, item.Travel, item.Phase, item.VerticalTravel)); break;
            }
        }
        Platforms = platforms.ToImmutableArray(); PlatformVisibility = BuildPlatformVisibility(Platforms); _supportStates = new TimedSupportState[Platforms.Length]; Pickups = pickups.ToArray(); Enemies = enemies.ToArray();
        Hazards = hazards.ToArray(); Mechanisms = mechanisms.ToArray();
        Checkpoints = checkpoints.OrderBy(c => c.X).ToArray();
        var connected = document.Objects.ToArray().Where(o => o.Kind == LevelObjectKind.Pipe && o.PipeLink != 0).ToArray();
        Pipes = connected.Select(o => o.Bounds).ToArray(); PipeLinks = connected.Select(o => o.PipeLink).ToArray();
        CoinCount = Pickups.Count(p => !p.IsRelic);
    }

    public Level(int index, bool isDungeon = false)
    {
        if ((uint)index >= Names.Length) throw new ArgumentOutOfRangeException(nameof(index));
        Index = index;
        IsDungeon = isDungeon;
        var platforms = new List<Platform>();
        var pickups = new List<Pickup>();
        var enemies = new List<Enemy>();
        var hazards = new List<Box>();
        var checkpoints = new List<Checkpoint>();
        var mechanisms = new List<Mechanism>();
        if (isDungeon)
        {
            float roomX = 0;
            var vault = CampaignRoute.VaultForWorld(index);
            for (int room = 0; room < vault.Length; room++)
            {
                var score = vault[room]; float floorY = 600 - score.Elevation;
                CampaignRoute.AddTerrain(score, roomX, floorY, platforms);
                CampaignRoute.AddEncounter(score, roomX, floorY, platforms, hazards, mechanisms);
                // Vault rewards are bonus coins; the three campaign relics remain
                // in the authored overworld encounters and cannot be farmed by pipes.
                CampaignRoute.AddRewards(index, room + 20, score, roomX, floorY, platforms, pickups, enemies);
                if (room + 1 < vault.Length) roomX += score.Width + score.Gap;
            }
            float end = roomX + vault[^1].Width - 200;
            Pipes = [new(65, 504, 100, 96), new(end, 504, 100, 96)];
            foreach (var pipe in Pipes) platforms.Add(new(pipe, PlatformKind.Pipe));
            platforms.Add(new(new(0, 80, end + 300, 44), PlatformKind.Stone));
            Spawn = new(210, 530); Exit = new(end + 100, 600);
            Platforms = platforms.ToImmutableArray(); PlatformVisibility = BuildPlatformVisibility(Platforms); _supportStates = new TimedSupportState[Platforms.Length]; Pickups = pickups.ToArray(); Enemies = enemies.ToArray();
            Hazards = hazards.ToArray(); Checkpoints = [];
            Mechanisms = mechanisms.ToArray(); CoinCount = Pickups.Length;
            return;
        }
        float x = 0;
        float lastY = 600;
        Box shortcutPipe = default;
        var route = CampaignRoute.ForWorld(index);
        int sections = route.Length;
        for (int section = 0; section < sections; section++)
        {
            bool last = section == sections - 1;
            var score = route[section];
            float width = score.Width;
            float y = 600 - score.Elevation;
            CampaignRoute.AddTerrain(score, x, y, platforms);
            if (section == 3 || section == sections - 4) checkpoints.Add(new(x + 95, y));
            CampaignRoute.AddEncounter(score, x, y, platforms, hazards, mechanisms);
            CampaignRoute.AddRewards(index, section, score, x, y, platforms, pickups, enemies);
            if (section == sections - 2) shortcutPipe = new(x + 48, y - 96, 100, 96);
            if (!last)
            {
                float gap = score.Gap;
                for (int c = 0; c < 3; c++)
                    pickups.Add(new() { Position = new(x + width - 20 + c * (gap + 40) / 2,
                        y - 100 - MathF.Sin(c * MathF.PI / 2) * 35) });
                x += width + gap;
            }
            lastY = y;
        }
        Pipes = [new(580, 504, 100, 96), shortcutPipe];
        Mechanisms = mechanisms.ToArray();
        foreach (var pipe in Pipes) platforms.Add(new(pipe, PlatformKind.Pipe));
        Platforms = platforms.ToImmutableArray(); PlatformVisibility = BuildPlatformVisibility(Platforms); _supportStates = new TimedSupportState[Platforms.Length]; Pickups = pickups.ToArray(); Enemies = enemies.ToArray();
        Hazards = hazards.ToArray(); Checkpoints = checkpoints.ToArray();
        Exit = new(x + 615, lastY);
        CoinCount = Pickups.Count(p => !p.IsRelic);
    }
}
