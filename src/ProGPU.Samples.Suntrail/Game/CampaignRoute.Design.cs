using System.Numerics;

namespace ProGPU.Samples.Suntrail.Game;

internal static partial class CampaignRoute
{
    public static void AddTerrain(RouteSection section, float x, float y, List<Platform> platforms)
    {
        float width = section.Width;
        void Ground(float start, float length) => platforms.Add(new(new(x + start, y, length, 510), PlatformKind.Ground));
        switch (section.Encounter)
        {
            case EncounterKind.FerryCrossing:
                Ground(0, 200); Ground(width - 200, 200);
                platforms.Add(new(new(x + width / 2 - 80, y - 24, 160, 24), PlatformKind.Moving, 58, .3f));
                break;
            case EncounterKind.TwinFerries:
                Ground(0, 200); Ground(width - 200, 200);
                platforms.Add(new(new(x + 248, y - 34, 132, 24), PlatformKind.Moving, 34, .2f));
                platforms.Add(new(new(x + width - 380, y - 56, 132, 24), PlatformKind.Moving, 34, MathF.PI + .2f));
                break;
            case EncounterKind.CrumbleBridge:
            case EncounterKind.SkyRelay:
                Ground(0, 200); Ground(width - 200, 200);
                // Four independent supports make commitment matter. The sky relay
                // varies height and includes a moving landing instead of four clones.
                for (int i = 0; i < 4; i++)
                {
                    float start = 212 + i * (width - 424) / 3;
                    float elevation = section.Encounter == EncounterKind.SkyRelay ? 32 + (i % 3) * 26 : 24 + (i % 2) * 12;
                    platforms.Add(new(new(x + start, y - elevation, 88, 24),
                        section.Encounter == EncounterKind.SkyRelay && i == 2 ? PlatformKind.Moving : PlatformKind.Crumble,
                        0, i * .7f, section.Encounter == EncounterKind.SkyRelay && i == 2 ? 18 : 0));
                }
                break;
            case EncounterKind.SplitCauseway:
                Ground(0, width * .28f); Ground(width * .40f, width * .20f); Ground(width * .72f, width * .28f);
                break;
            default: Ground(0, width); break;
        }
    }

    public static void AddRewards(int world, int sectionIndex, RouteSection section, float x, float y,
        List<Platform> platforms, List<Pickup> pickups, List<Enemy> enemies)
    {
        float width = section.Width, center = x + width / 2;
        bool crossing = section.Encounter is EncounterKind.FerryCrossing or EncounterKind.TwinFerries or
            EncounterKind.CrumbleBridge or EncounterKind.SkyRelay or EncounterKind.SplitCauseway;
        int count = sectionIndex == 0 ? 5 : crossing ? 8 : 6;
        for (int i = 0; i < count; i++)
        {
            float px = sectionIndex == 0 ? x + 370 + i * 35 : x + 105 + i * (width - 210) / (count - 1);
            float py = y - (crossing ? 82 : 62) - MathF.Sin(i * MathF.PI / (count - 1)) * (crossing ? 30 : 18);
            pickups.Add(new() { Position = new(px, py) });
        }
        if (sectionIndex is > 0 and < 20 && width >= 320 && section.Encounter == EncounterKind.Open)
        {
            var kind = world switch { 2 or 3 or 7 => EnemyKind.Hoverer, 4 or 5 => EnemyKind.Hopper, _ => EnemyKind.Walker };
            var enemy = new Enemy { Position = new(x + width - 130, y - 34), Left = x + 100, Right = x + width - 64,
                Speed = -(50 + world * 7), AnchorY = y - 34, Phase = sectionIndex * .27f, Kind = kind };
            enemy.Position.Y += enemy.VerticalOffset(0); enemies.Add(enemy);
        }
        if (sectionIndex is not (1 or 4 or 7)) return;
        Vector2 reward;
        switch (section.Encounter)
        {
            case EncounterKind.CanopyFork: reward = new(center + 30, y - 200); break;
            case EncounterKind.BrokenArches: reward = new(center + 125, y - 222); break;
            case EncounterKind.SawSlalom: reward = new(center + 145, y - 164); break;
            case EncounterKind.SpringGrove: reward = new(center + 130, y - 286); break;
            case EncounterKind.FlameWeave: reward = new(center - 12, y - 152); break;
            case EncounterKind.CrystalStairs: reward = new(x + 530, y - 270); break;
            case EncounterKind.CrumbleBridge: case EncounterKind.SkyRelay:
                reward = new(center + 20, y - 116); break;
            case EncounterKind.TwinFerries: reward = new(x + width - 308, y - 112); break;
            case EncounterKind.FerryCrossing: reward = new(center, y - 84); break;
            case EncounterKind.IceBraking:
                platforms.Add(new(new(x + 140, y - 104, 120, 24), PlatformKind.Ledge));
                platforms.Add(new(new(center - 54, y - 158, 120, 24), PlatformKind.Moving, 0, 1, 36));
                platforms.Add(new(new(x + width - 210, y - 218, 120, 24), PlatformKind.Ledge));
                reward = new(x + width - 150, y - 264); break;
            case EncounterKind.ConveyorPress: reward = new(x + 110, y - 122); break;
            default:
                platforms.Add(new(new(x + 120, y - 80, 116, 24), PlatformKind.Ledge));
                platforms.Add(new(new(x + 280, y - 156, 112, 24), PlatformKind.Ledge));
                reward = new(x + 330, y - 202); break;
        }
        pickups.Add(new() { Position = reward, IsRelic = true });
    }

    // Original optional rooms. Every score begins and ends on safe 600-unit floors
    // for paired pipes, but the interior silhouette, tempo and mechanics differ.
    private static readonly RouteSection[][] Vaults =
    [
        [new(620,0,80), new(640,-48,80,EncounterKind.CanopyFork), new(660,0,80,EncounterKind.FerryCrossing), new(600,0,0)],
        [new(620,0,80), new(740,64,80,EncounterKind.BrokenArches), new(760,96,80,EncounterKind.ConveyorPress), new(380,48,80), new(600,0,0)],
        [new(620,0,80), new(720,48,80,EncounterKind.CrystalStairs), new(720,96,80,EncounterKind.CrumbleBridge), new(740,48,80,EncounterKind.FlameWeave), new(600,0,0)],
        [new(620,0,100), new(840,0,100,EncounterKind.TwinFerries), new(660,-48,90,EncounterKind.SplitCauseway), new(680,0,80,EncounterKind.FerryCrossing), new(600,0,0)],
        [new(620,0,80), new(720,64,80,EncounterKind.SpringGrove), new(760,96,80,EncounterKind.CanopyFork), new(700,48,80,EncounterKind.CrumbleBridge), new(600,0,0)],
        [new(620,0,80), new(760,32,80,EncounterKind.IceBraking), new(660,80,80,EncounterKind.Tunnel), new(700,32,80,EncounterKind.SplitCauseway), new(600,0,0)],
        [new(620,0,80), new(780,48,80,EncounterKind.FlameWeave), new(760,96,80,EncounterKind.ConveyorPress), new(720,48,80,EncounterKind.CrusherHall), new(600,0,0)],
        [new(620,0,80), new(760,48,80,EncounterKind.SkyRelay), new(820,96,80,EncounterKind.TwinFerries), new(760,48,80,EncounterKind.SkyRelay), new(600,0,0)]
    ];
    public static ReadOnlySpan<RouteSection> VaultForWorld(int world) => Vaults[world];
}
