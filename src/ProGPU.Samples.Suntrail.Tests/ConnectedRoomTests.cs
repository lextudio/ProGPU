using System.Text;
using ProGPU.Samples.Suntrail.Game;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class ConnectedRoomTests
{
    [Fact]
    public void LinkedTrailRoundtripRetainsEveryRoomBiomeAndEndpoint()
    {
        var original = LevelDocument.CreateLinkedStarter();
        var bytes = LevelFiles.Write(original);
        var loaded = LevelFiles.Read(bytes, "rooms.suntrail");
        Assert.Equal(3, loaded.RoomCount);
        for (int i = 0; i < original.RoomCount; i++)
        {
            var expected = original.GetRoom(i); var actual = loaded.GetRoom(i);
            Assert.Equal(expected.Name, actual.Name); Assert.Equal(expected.Biome, actual.Biome);
            Assert.Equal(expected.IsDungeon, actual.IsDungeon);
            Assert.Equal(expected.Objects.ToArray(), actual.Objects.ToArray());
        }
        Assert.Equal(bytes, LevelFiles.Write(loaded));
        Assert.Equal(bytes, LevelFiles.Write(new LevelEditor(loaded).Snapshot()));
    }

    [Fact]
    public void LegacySingleRoomVersionStillLoads()
    {
        var json = """
            {"format":"suntrail","version":1,"name":"Legacy","biome":0,"objects":[
              {"kind":"spawn","x":140,"y":552},
              {"kind":"ground","x":0,"y":600,"width":900,"height":500},
              {"kind":"exit","x":800,"y":600}]}
            """;
        var room = LevelFiles.Read(Encoding.UTF8.GetBytes(json), "legacy.suntrail");
        Assert.Equal(1, room.RoomCount); Assert.False(room.IsDungeon);
        Assert.Equal(3, room.Objects.Length);
    }

    [Fact]
    public void RoomEditsAndPipeDeletionUndoAsWholeTrailTransactions()
    {
        var editor = new LevelEditor(LevelDocument.CreateLinkedStarter());
        byte[] original = LevelFiles.Write(editor.Snapshot());
        editor.SwitchRoom(1); editor.SetBiome(6); editor.ToggleDungeon();
        Assert.Equal(6, editor.Snapshot().GetRoom(1).Biome);
        editor.Undo(); editor.Undo(); Assert.Equal(original, LevelFiles.Write(editor.Snapshot()));
        editor.Select(3); editor.DeleteSelected();
        Assert.DoesNotContain(editor.Snapshot().Objects.ToArray(), o => o.PipeLink == 1);
        editor.Undo(); Assert.Equal(original, LevelFiles.Write(editor.Snapshot()));
        editor.DeleteRoom(); Assert.Equal(2, editor.RoomCount);
        Assert.DoesNotContain(editor.Snapshot().GetRoom(1).Objects.ToArray(), o => o.PipeLink == 3);
        editor.Undo(); Assert.Equal(original, LevelFiles.Write(editor.Snapshot()));
        editor.Redo(); Assert.Equal(2, editor.RoomCount);
    }

    [Fact]
    public void NewRoomCannotSaveUntilConnectedAndLinkingIsUndoable()
    {
        var editor = new LevelEditor(LevelDocument.CreateStarter());
        editor.Add(LevelObjectKind.Pipe, new(416, 496)); int first = editor.Selected;
        editor.AddRoom(); editor.Add(LevelObjectKind.Pipe, new(112, 496));
        Assert.Throws<FormatException>(() => editor.Snapshot());
        editor.ConnectPipes(0, first);
        var document = editor.Snapshot();
        int link = document.Objects[first].PipeLink;
        Assert.True(link > 0);
        Assert.Equal(link, document.GetRoom(1).Objects[editor.Selected].PipeLink);
        editor.Undo(); Assert.Throws<FormatException>(() => editor.Snapshot());
        editor.Redo(); Assert.Equal(2, editor.Snapshot().RoomCount);
        editor.DisconnectSelectedPipe(); Assert.Throws<FormatException>(() => editor.Snapshot());
        editor.Undo(); Assert.Equal(2, editor.Snapshot().RoomCount);
    }

    [Fact]
    public void InvalidGraphsFailBeforeChangingTheRunningSession()
    {
        var session = new GameSession(); session.StartLevel(0); var before = session.Level;
        var starter = LevelDocument.CreateStarter();
        var unreachable = new LevelDocument("Unreachable", 0, starter.Objects, rooms: [starter]);
        Assert.Throws<FormatException>(() => session.StartDocument(unreachable));
        Assert.Same(before, session.Level);
        Assert.Throws<FormatException>(() => LevelFiles.Write(unreachable));
        var dangling = new LevelDocument("Dangling", 0,
            [.. starter.Objects, new(LevelObjectKind.Pipe, new(300, 504, 96, 96), PipeLink: 7)]);
        Assert.Throws<FormatException>(() => LevelFiles.Write(dangling));
    }

    [Fact]
    public void PipeVisitsRetainPickupsAndRespawnUsesCheckpointRoom()
    {
        static LevelObject[] Objects(int link, bool checkpoint) =>
        [
            new(LevelObjectKind.Spawn, new(145, 456, 0, 0)),
            new(LevelObjectKind.Ground, new(0, 600, 900, 500)),
            new(LevelObjectKind.Pipe, new(112, 504, 96, 96), PipeLink: link),
            new(LevelObjectKind.Coin, new(160, 475, 0, 0)),
            new(checkpoint ? LevelObjectKind.Checkpoint : LevelObjectKind.Coin, new(145, 504, 0, 0)),
            new(LevelObjectKind.Exit, new(800, 600, 0, 0))
        ];
        var vault = new LevelDocument("Vault", 2, Objects(1, true), true);
        var document = new LevelDocument("Entrance", 0, Objects(1, false), rooms: [vault]);
        var session = new GameSession(); session.StartDocument(document); var entrance = session.Level;
        void Settle() { for (int i = 0; i < 5; i++) session.Step(default); Assert.True(session.CanUsePipe); }
        Settle(); session.Step(new(0, false, false, false, true));
        Assert.Equal(1, session.RoomIndex); var destination = session.Level;
        Settle(); Assert.Equal(0, session.CheckpointIndex); int coins = session.Coins;
        session.Step(new(0, false, false, false, true));
        Assert.Same(entrance, session.Level);
        Settle(); session.Step(new(0, false, false, false, true));
        Assert.Same(destination, session.Level); Assert.Equal(coins, session.Coins);
        Settle(); session.Step(new(0, false, false, false, true));
        session.Respawn(); Assert.Same(destination, session.Level); Assert.Equal(1, session.RoomIndex);
        Assert.Equal(new System.Numerics.Vector2(145, 456), session.Position);
        Assert.Equal(0, session.UnlockedLevel);
    }
}
