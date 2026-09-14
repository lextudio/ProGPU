using ProGPU.GameEngine.Simulation;
using ProGPU.Samples.Suntrail.Game;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SurfaceMechanicsTests
{
    [Fact]
    public void ContactResponseConvergesWithoutOvershootAndPreservesBeltDirection()
    {
        var belt = new ContactSurface(2300, 2300, -110);
        float speed = 0;
        for (int i = 0; i < 120; i++) speed = belt.IntegrateTangent(speed, 0, 1f / 120);
        Assert.Equal(-110, speed);
        for (int i = 0; i < 120; i++) speed = belt.IntegrateTangent(speed, 300, 1f / 120);
        Assert.Equal(190, speed);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContactSurface(float.NaN, 10));
    }

    [Fact]
    public void IceRetainsMomentumWhenOrdinaryGroundHasStopped()
    {
        var ice = new Platform(default, PlatformKind.Ice).Contact;
        var ground = new Platform(default, PlatformKind.Ground).Contact;
        float sliding = 300, stopped = 300;
        for (int i = 0; i < 60; i++)
        {
            sliding = ice.IntegrateTangent(sliding, 0, GameSession.StepSeconds);
            stopped = ground.IntegrateTangent(stopped, 0, GameSession.StepSeconds);
        }
        Assert.Equal(0, stopped); Assert.InRange(sliding, 219, 221);
        Assert.InRange(ice.IntegrateTangent(sliding, -300, .1f), 172, 176);
    }

    [Fact]
    public void SupportWarningAndRecoveryUseFixedTicksAndRepeatedContactCannotDelayCollapse()
    {
        var policy = new TimedSupportPolicy(66, 300);
        var support = new TimedSupportState();
        Assert.True(support.IsSolid(1000, policy)); Assert.True(support.Touch(1000, policy));
        Assert.False(support.Touch(1065, policy)); Assert.True(support.IsSolid(1065, policy));
        Assert.False(support.IsSolid(1066, policy)); Assert.False(support.IsSolid(1365, policy));
        Assert.True(support.IsSolid(1366, policy)); Assert.Equal(0, support.WarningProgress(1366, policy));
        Assert.True(support.Touch(1366, policy)); support.Reset(); Assert.True(support.IsSolid(1432, policy));
    }

    [Fact]
    public void AutomaticSpringLaunchDoesNotDependOnHoldingJump()
    {
        var session = StartOn(PlatformKind.Spring);
        float minimum = session.Position.Y;
        bool launched = false;
        for (int tick = 0; tick < 95; tick++)
        {
            session.Step(default); minimum = Math.Min(minimum, session.Position.Y);
            launched |= session.Velocity.Y < -900;
        }
        Assert.True(launched); Assert.InRange(minimum, 175, 210);
    }

    [Fact]
    public void ConveyorMovesAnIdlePlayerWithoutMovingItsGeometry()
    {
        var session = StartOn(PlatformKind.Conveyor); float start = session.Position.X;
        for (int tick = 0; tick < 60; tick++) session.Step(default);
        Assert.InRange(session.Position.X - start, 48, 56);
        var belt = session.Level.Platforms.Single(p => p.Kind == PlatformKind.Conveyor);
        Assert.Equal(belt.Bounds, belt.At(17)); Assert.Equal(belt.Bounds, belt.At(29));
    }

    [Fact]
    public void CollapsedPlatformStopsSupportingPlayerAndRespawnRestoresIt()
    {
        var session = StartOn(PlatformKind.Crumble);
        for (int tick = 0; tick < 100; tick++) session.Step(default);
        int index = Array.FindIndex(session.Level.Platforms.ToArray(), p => p.Kind == PlatformKind.Crumble);
        Assert.False(session.Level.IsPlatformSolid(index, session.Tick)); Assert.True(session.Position.Y > 480);
        session.Respawn(); Assert.True(session.Level.IsPlatformSolid(index, session.Tick));
        Assert.Equal(452, session.Position.Y);
    }

    [Theory]
    [InlineData(LevelObjectKind.Spring)] [InlineData(LevelObjectKind.Conveyor)]
    [InlineData(LevelObjectKind.Ice)] [InlineData(LevelObjectKind.Crumble)]
    public void NewSurfaceKindsSurviveAuthoringRoundtrip(LevelObjectKind kind)
    {
        var editor = new LevelEditor(LevelDocument.CreateStarter()); editor.Add(kind, new(320, 480));
        if (kind == LevelObjectKind.Conveyor) editor.ReverseConveyor();
        var document = LevelFiles.Read(LevelFiles.Write(editor.Snapshot()), "surfaces.suntrail");
        Assert.Equal(editor.Objects[^1], document.Objects[^1]);
        Assert.Contains(document.CreateLevel().Platforms, p => p.Kind.ToString() == kind.ToString());
    }

    private static GameSession StartOn(PlatformKind kind)
    {
        var objectKind = kind switch
        {
            PlatformKind.Spring => LevelObjectKind.Spring, PlatformKind.Conveyor => LevelObjectKind.Conveyor,
            PlatformKind.Ice => LevelObjectKind.Ice, _ => LevelObjectKind.Crumble
        };
        var document = new LevelDocument("Surface fixture", 0,
        [
            new(LevelObjectKind.Spawn, new(250, 452, 0, 0)),
            new(LevelObjectKind.Ground, new(0, 800, 1000, 500)),
            new(objectKind, new(100, 500, 500, 32), kind == PlatformKind.Conveyor ? 110 : 0),
            new(LevelObjectKind.Exit, new(900, 800, 0, 0))
        ]);
        var session = new GameSession(); session.StartDocument(document); return session;
    }
}
