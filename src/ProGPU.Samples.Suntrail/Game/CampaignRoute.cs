namespace ProGPU.Samples.Suntrail.Game;

internal enum EncounterKind
{
    Open, Steps, Tunnel, Brambles, SawCrossing, FlameGate, CrusherHall, SpringGrove, ConveyorRun, IceRun, CrumbleSteps,
    CanopyFork, BrokenArches, FerryCrossing, TwinFerries, CrumbleBridge, SplitCauseway,
    SawSlalom, FlameWeave, ConveyorPress, IceBraking, CrystalStairs, SkyRelay
}
internal readonly record struct RouteSection(float Width, float Elevation, float Gap, EncounterKind Encounter = EncounterKind.Open);

/// <summary>Original authored route scores. Distances are world units; generation is O(S) at level load.</summary>
internal static partial class CampaignRoute
{
    // Each score has its own spacing, rests, narrow crossings and encounter order.
    // Short islands have no generic gallery, while long rooms can carry upper routes.
    private static readonly RouteSection[][] Routes =
    [
        // Orchard: forgiving boughs become creek ferries, then a canopy/lower fork.
        [new(930,0,100), new(700,0,84,EncounterKind.CanopyFork), new(420,48,76),
         new(680,48,96,EncounterKind.FerryCrossing), new(760,0,120,EncounterKind.SpringGrove),
         new(340,24,100,EncounterKind.Brambles), new(740,0,80,EncounterKind.Tunnel),
         new(760,48,100,EncounterKind.CanopyFork), new(350,72,84),
         new(680,24,100,EncounterKind.CrumbleBridge), new(850,0,0)],
        // Aqueduct: broken piers, roof/lower passages, ferries and old machinery.
        [new(930,0,90), new(760,32,100,EncounterKind.BrokenArches), new(300,64,80),
         new(680,96,90,EncounterKind.SplitCauseway), new(740,48,120,EncounterKind.SawSlalom),
         new(360,0,80), new(860,32,96,EncounterKind.TwinFerries),
         new(800,80,105,EncounterKind.ConveyorPress), new(320,112,80),
         new(760,64,100,EncounterKind.BrokenArches), new(600,24,100), new(850,0,0)],
        // Cathedral: crystalline switchbacks, timed fire and fragile upper galleries.
        [new(930,0,100), new(740,48,90,EncounterKind.CrystalStairs), new(340,96,90),
         new(760,144,110,EncounterKind.FlameWeave), new(740,96,80,EncounterKind.CrumbleBridge),
         new(360,48,100), new(780,0,90,EncounterKind.BrokenArches),
         new(760,48,100,EncounterKind.CrystalStairs), new(340,96,85),
         new(680,128,100,EncounterKind.FerryCrossing), new(850,64,0)],
        // Coast: broad water openings, irregular piers and two independent ferries.
        [new(930,0,120), new(840,0,115,EncounterKind.TwinFerries), new(320,24,100),
         new(680,48,110,EncounterKind.SplitCauseway), new(760,0,120,EncounterKind.BrokenArches),
         new(340,24,105), new(720,0,110,EncounterKind.CrumbleBridge),
         new(840,48,100,EncounterKind.TwinFerries), new(380,80,115),
         new(740,32,110,EncounterKind.SawSlalom), new(320,0,110), new(850,0,0)],
        // Highlands: spring shortcuts and thorny low routes with long leafy landings.
        [new(930,0,90), new(740,48,80,EncounterKind.CanopyFork), new(360,96,80),
         new(720,144,100,EncounterKind.SpringGrove), new(760,96,115,EncounterKind.CrumbleBridge),
         new(340,48,95), new(700,96,80,EncounterKind.Brambles),
         new(780,144,100,EncounterKind.CanopyFork), new(360,96,80),
         new(720,48,100,EncounterKind.SawSlalom), new(850,0,0)],
        // Glacier: alternate traction and braking pads, then commit to icy islands.
        [new(930,0,105), new(760,32,100,EncounterKind.IceBraking), new(320,80,85),
         new(720,128,90,EncounterKind.SplitCauseway), new(780,80,115,EncounterKind.IceBraking),
         new(360,24,90), new(740,64,95,EncounterKind.Tunnel),
         new(780,112,105,EncounterKind.CrystalStairs), new(340,64,100),
         new(700,24,100,EncounterKind.CrumbleBridge), new(850,0,0)],
        // Forge: staggered heat gates, pressing belts and high refuge shelves.
        [new(930,0,100), new(780,32,110,EncounterKind.FlameWeave), new(360,80,90),
         new(760,48,100,EncounterKind.ConveyorPress), new(780,0,120,EncounterKind.FlameWeave),
         new(360,48,90), new(780,96,100,EncounterKind.CrusherHall),
         new(800,48,115,EncounterKind.ConveyorPress), new(360,0,100),
         new(760,56,100,EncounterKind.CrumbleBridge), new(850,0,0)],
        // Sky: the final route combines fragile relays, ferries and changing heights.
        [new(930,0,105), new(780,48,110,EncounterKind.SkyRelay), new(340,96,90),
         new(720,144,105,EncounterKind.FerryCrossing), new(820,96,120,EncounterKind.CrusherHall),
         new(340,48,100), new(760,96,90,EncounterKind.SpringGrove),
         new(840,144,110,EncounterKind.TwinFerries), new(360,96,100),
         new(780,48,110,EncounterKind.FlameWeave), new(360,0,90),
         new(780,48,105,EncounterKind.SkyRelay), new(850,0,0)]
    ];

    public static ReadOnlySpan<RouteSection> ForWorld(int world) => Routes[world];

    public static void AddEncounter(RouteSection section, float x, float y,
        List<Platform> platforms, List<Box> hazards, List<Mechanism> mechanisms)
    {
        float center = x + section.Width * .5f;
        switch (section.Encounter)
        {
            case EncounterKind.CanopyFork:
                platforms.Add(new(new(x + 135, y - 78, 116, 24), PlatformKind.Ledge));
                platforms.Add(new(new(center - 42, y - 150, 150, 24), PlatformKind.Moving, 0, .4f, 24));
                platforms.Add(new(new(x + section.Width - 180, y - 82, 110, 24), PlatformKind.Ledge));
                hazards.Add(new(center - 78, y - 22, 46, 22));
                hazards.Add(new(center + 96, y - 22, 46, 22));
                break;
            case EncounterKind.BrokenArches:
                platforms.Add(new(new(x + 90, y - 64, 80, 24), PlatformKind.Ledge));
                platforms.Add(new(new(x + 180, y - 130, 160, 36), PlatformKind.Stone));
                platforms.Add(new(new(center + 60, y - 174, 154, 36), PlatformKind.Stone));
                platforms.Add(new(new(x + section.Width - 150, y - 84, 90, 24), PlatformKind.Ledge));
                mechanisms.Add(new(new(center + 46, y - 42, 42, 42), MechanismKind.Saw, .7f, 54));
                break;
            case EncounterKind.SawSlalom:
                platforms.Add(new(new(x + 145, y - 76, 110, 24), PlatformKind.Ledge));
                platforms.Add(new(new(center + 80, y - 118, 140, 24), PlatformKind.Ledge));
                mechanisms.Add(new(new(center - 80, y - 42, 42, 42), MechanismKind.Saw, 0, 48));
                mechanisms.Add(new(new(center + 105, y - 214, 42, 42), MechanismKind.Saw, 2, 65));
                break;
            case EncounterKind.FlameWeave:
                // Three distinct phases leave waiting bays between each flame.
                for (int i = 0; i < 3; i++)
                    mechanisms.Add(new(new(center - 140 + i * 140, y - 90, 28, 90), MechanismKind.FlameJet, i / 3f));
                platforms.Add(new(new(center - 60, y - 108, 90, 24), PlatformKind.Ledge));
                platforms.Add(new(new(x + section.Width - 198, y - 168, 116, 24), PlatformKind.Moving, 0, 1.2f, 30));
                break;
            case EncounterKind.ConveyorPress:
                platforms.Add(new(new(x + 140, y - 24, section.Width - 280, 24), PlatformKind.Conveyor, -165));
                mechanisms.Add(new(new(center - 80, y - 304, 64, 80), MechanismKind.Crusher, .12f, 200));
                mechanisms.Add(new(new(center + 120, y - 304, 64, 80), MechanismKind.Crusher, .62f, 200));
                platforms.Add(new(new(x + 66, y - 76, 90, 24), PlatformKind.Ledge));
                platforms.Add(new(new(x + section.Width - 160, y - 100, 90, 24), PlatformKind.Ledge));
                break;
            case EncounterKind.IceBraking:
                for (int i = 0; i < 3; i++)
                {
                    float start = x + 90 + i * 190;
                    platforms.Add(new(new(start, y - 16, 145, 24), PlatformKind.Ice));
                    platforms.Add(new(new(start + 145, y - 48, 44, 48), PlatformKind.Stone));
                }
                break;
            case EncounterKind.CrystalStairs:
                platforms.Add(new(new(x + 135, y - 88, 116, 24), PlatformKind.Ledge));
                platforms.Add(new(new(x + 300, y - 164, 110, 24), PlatformKind.Crumble));
                platforms.Add(new(new(x + 470, y - 224, 132, 24), PlatformKind.Ledge));
                platforms.Add(new(new(x + section.Width - 150, y - 92, 96, 24), PlatformKind.Ledge));
                hazards.Add(new(center - 32, y - 22, 64, 22));
                break;
            case EncounterKind.SpringGrove:
                // A spring opens a high shortcut beyond the ordinary jump apex.
                platforms.Add(new(new(center - 40, y - 48, 80, 48), PlatformKind.Spring));
                platforms.Add(new(new(center + 75, y - 240, 130, 24), PlatformKind.Ledge));
                platforms.Add(new(new(center - 205, y - 180, 112, 24), PlatformKind.Ledge));
                break;
            case EncounterKind.ConveyorRun:
                platforms.Add(new(new(x + 150, y - 24, section.Width - 300, 24), PlatformKind.Conveyor, -110));
                hazards.Add(new(center + 90, y - 46, 42, 22));
                break;
            case EncounterKind.IceRun:
                // Broad run-in teaches braking; a short raised stop marks the exit.
                platforms.Add(new(new(x + 100, y - 16, section.Width - 200, 24), PlatformKind.Ice));
                platforms.Add(new(new(x + section.Width - 75, y - 48, 48, 48), PlatformKind.Stone));
                break;
            case EncounterKind.CrumbleSteps:
                platforms.Add(new(new(center - 175, y - 88, 100, 24), PlatformKind.Crumble));
                platforms.Add(new(new(center - 15, y - 144, 100, 24), PlatformKind.Crumble));
                platforms.Add(new(new(center + 145, y - 200, 100, 24), PlatformKind.Crumble));
                break;
            case EncounterKind.Steps:
                platforms.Add(new(new(center - 100, y - 48, 64, 48), PlatformKind.Stone));
                platforms.Add(new(new(center - 36, y - 88, 90, 88), PlatformKind.Stone));
                break;
            case EncounterKind.Tunnel:
                // A solid roof changes the jump envelope, with clear entrances and exits.
                platforms.Add(new(new(x + 145, y - 150, section.Width - 290, 36), PlatformKind.Stone));
                break;
            case EncounterKind.Brambles:
                hazards.Add(new(center - 100, y - 22, 48, 22));
                hazards.Add(new(center + 130, y - 22, 48, 22));
                break;
            case EncounterKind.SawCrossing:
                mechanisms.Add(new(new(center - 22, y - 42, 42, 42), MechanismKind.Saw, .4f, 48));
                break;
            case EncounterKind.FlameGate:
                mechanisms.Add(new(new(center, y - 90, 30, 90), MechanismKind.FlameJet, .23f));
                break;
            case EncounterKind.CrusherHall:
                mechanisms.Add(new(new(center, y - 275, 64, 80), MechanismKind.Crusher, .4f, 195));
                break;
        }
    }
}
