using System.Numerics;

namespace ProGPU.Samples.Suntrail.Game;

/// <summary>CPU-only editor with 64 bounded undo snapshots and one transaction per drag.</summary>
public sealed class LevelEditor
{
    private sealed record Room(string Name, int Biome, bool IsDungeon, LevelObject[] Objects);
    private sealed record State(Room[] Rooms, int ActiveRoom)
    {
        public LevelObject[] Objects => Rooms[ActiveRoom].Objects;
    }
    private readonly List<Room> _rooms = [];
    public int RoomIndex { get; private set; }
    public int RoomCount => _rooms.Count;
    public bool IsDungeon { get; private set; }
    public int Revision { get; private set; }
    private readonly List<State> _undo = [], _redo = [];
    private readonly List<LevelObject> _objects = [];
    private State? _drag;
    public string Name { get; private set; } = "";
    public int Biome { get; private set; }
    public int Selected { get; private set; } = -1;
    public IReadOnlyList<LevelObject> Objects => _objects;
    public bool CanUndo => _undo.Count != 0;
    public bool CanRedo => _redo.Count != 0;
    public bool IsDragging => _drag is not null;
    public event Action? Changed;
    public LevelEditor(LevelDocument document) => Load(document);
    public void Load(LevelDocument document)
    {
        Revision++; _drag = null; _undo.Clear(); _redo.Clear(); Selected = -1;
        var rooms = new Room[document.RoomCount];
        for (int i = 0; i < rooms.Length; i++)
        {
            var room = document.GetRoom(i);
            rooms[i] = new(room.Name, room.Biome, room.IsDungeon, room.Objects.ToArray());
        }
        Restore(new(rooms, 0));
    }
    public LevelDocument Snapshot()
    {
        var state = Capture();
        var children = new LevelDocument[state.Rooms.Length - 1];
        for (int i = 1; i < state.Rooms.Length; i++) children[i - 1] = Document(state.Rooms[i]);
        var root = state.Rooms[0];
        var result = new LevelDocument(root.Name, root.Biome, root.Objects, root.IsDungeon, children);
        result.ValidateConnections();
        return result;
    }
    private static LevelDocument Document(Room room) => new(room.Name, room.Biome, room.Objects, room.IsDungeon);
    private State Capture()
    {
        var rooms = _rooms.ToArray();
        rooms[RoomIndex] = new(Name, Biome, IsDungeon, _objects.ToArray());
        return new(rooms, RoomIndex);
    }
    private void Restore(State state)
    {
        _rooms.Clear(); _rooms.AddRange(state.Rooms); RoomIndex = state.ActiveRoom;
        var room = state.Rooms[RoomIndex];
        Name = room.Name; Biome = room.Biome; IsDungeon = room.IsDungeon;
        _objects.Clear(); _objects.AddRange(room.Objects);
        Selected = Math.Min(Selected, _objects.Count - 1); Changed?.Invoke();
    }
    public void SwitchRoom(int index)
    {
        CancelDrag(); if ((uint)index >= _rooms.Count || index == RoomIndex) return;
        var state = Capture(); Selected = -1; Restore(state with { ActiveRoom = index });
    }
    public void AddRoom()
    {
        CancelDrag();
        if (_rooms.Count == LevelDocument.MaximumRooms) throw new FormatException("A trail supports at most eight rooms.");
        var state = Capture(); Remember(state);
        var starter = LevelDocument.CreateStarter();
        var rooms = state.Rooms.Append(new Room($"Room {_rooms.Count + 1}", Biome, true, starter.Objects.ToArray())).ToArray();
        Selected = -1; Restore(new(rooms, rooms.Length - 1));
    }
    public void DeleteRoom()
    {
        CancelDrag();
        if (RoomIndex == 0) throw new FormatException("The first room is the trail entrance and cannot be deleted.");
        var state = Capture(); Remember(state);
        var links = state.Rooms[RoomIndex].Objects.Where(o => o.PipeLink != 0).Select(o => o.PipeLink).ToHashSet();
        var rooms = state.Rooms.Where((_, i) => i != RoomIndex).Select(r => r with
        { Objects = r.Objects.Select(o => links.Contains(o.PipeLink) ? o with { PipeLink = 0 } : o).ToArray() }).ToArray();
        Selected = -1; Restore(new(rooms, 0));
    }
    public void ToggleDungeon()
    {
        CancelDrag(); Remember(Capture()); IsDungeon = !IsDungeon; Changed?.Invoke();
    }
    public void ConnectPipes(int firstRoom, int firstObject)
    {
        CancelDrag(); var state = Capture();
        if ((uint)firstRoom >= state.Rooms.Length || (uint)firstObject >= state.Rooms[firstRoom].Objects.Length || Selected < 0)
            throw new FormatException("Select both pipe endpoints.");
        var first = state.Rooms[firstRoom].Objects[firstObject]; var second = _objects[Selected];
        if (first.Kind != LevelObjectKind.Pipe || second.Kind != LevelObjectKind.Pipe || (firstRoom == RoomIndex && firstObject == Selected))
            throw new FormatException("Choose two different pipes to connect.");
        if (first.Bounds.Y < GameSession.PlayerHeight || second.Bounds.Y < GameSession.PlayerHeight)
            throw new FormatException("Pipe entrances need space above them for the player.");
        var used = state.Rooms.SelectMany(r => r.Objects).Select(o => o.PipeLink).ToHashSet();
        int link = 1; while (used.Contains(link)) link++;
        var rooms = state.Rooms.Select((r, ri) => r with { Objects = r.Objects.Select((o, oi) =>
            (ri == firstRoom && oi == firstObject) || (ri == RoomIndex && oi == Selected) ? o with { PipeLink = link } :
            o.PipeLink != 0 && (o.PipeLink == first.PipeLink || o.PipeLink == second.PipeLink) ? o with { PipeLink = 0 } : o).ToArray() }).ToArray();
        // Validate endpoint geometry before recording an undo transaction.
        Document(rooms[firstRoom]); Document(rooms[RoomIndex]);
        Remember(state); Restore(new(rooms, RoomIndex));
    }
    public void DisconnectSelectedPipe()
    {
        CancelDrag(); if (Selected < 0 || _objects[Selected].PipeLink == 0) return;
        var state = Capture(); Remember(state); int link = _objects[Selected].PipeLink;
        Restore(new(ClearLink(state.Rooms, link), RoomIndex));
    }
    private static Room[] ClearLink(Room[] rooms, int link) => rooms.Select(r => r with
    { Objects = r.Objects.Select(o => o.PipeLink == link ? o with { PipeLink = 0 } : o).ToArray() }).ToArray();
    private void Remember(State state)
    {
        Revision++;
        if (_undo.Count == 64) _undo.RemoveAt(0);
        _undo.Add(state); _redo.Clear();
    }
    public void SetBiome(int biome)
    {
        if ((uint)biome >= 8 || Biome == biome) return;
        CancelDrag(); Remember(Capture()); Biome = biome; Changed?.Invoke();
    }
    public void Select(int index) { Selected = index >= 0 && index < _objects.Count ? index : -1; Changed?.Invoke(); }
    public int HitTest(Vector2 p)
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            var b = SelectionBounds(_objects[i]);
            if (p.X >= b.X && p.X <= b.Right && p.Y >= b.Y && p.Y <= b.Bottom) return i;
        }
        return -1;
    }
    public static Box SelectionBounds(LevelObject item) => item.Kind switch
    {
        LevelObjectKind.Coin or LevelObjectKind.Relic => new(item.Bounds.X - 15, item.Bounds.Y - 15, 30, 30),
        LevelObjectKind.Checkpoint => new(item.Bounds.X, item.Bounds.Y - 100, 30, 100),
        LevelObjectKind.Exit => new(item.Bounds.X, item.Bounds.Y - 150, 70, 150),
        LevelObjectKind.Spawn => new(item.Bounds.X, item.Bounds.Y, 30, 48),
        _ => item.Bounds
    };
    public void Add(LevelObjectKind kind, Vector2 p)
    {
        CancelDrag();
        if (_objects.Count == LevelDocument.MaximumObjects) throw new FormatException("The editor object limit is reached.");
        Remember(Capture());
        // Spawn/exit tools relocate their existing marker, preserving exactly one.
        int existing = kind is LevelObjectKind.Spawn or LevelObjectKind.Exit ? _objects.FindIndex(o => o.Kind == kind) : -1;
        var size = kind switch
        {
            LevelObjectKind.Ground => new Vector2(320, 500), LevelObjectKind.Ledge or LevelObjectKind.Moving => new(160, 24),
            LevelObjectKind.Spring => new(80, 48),
            LevelObjectKind.Conveyor or LevelObjectKind.Ice or LevelObjectKind.Crumble => new(192, 24),
            LevelObjectKind.Crate or LevelObjectKind.Stone => new(64, 64), LevelObjectKind.Pipe => new(96, 96),
            LevelObjectKind.Hazard => new(64, 24), LevelObjectKind.Enemy or LevelObjectKind.Hopper or LevelObjectKind.Hoverer => new(42, 34),
            LevelObjectKind.Saw => new(42, 42), LevelObjectKind.Flame => new(30, 90), LevelObjectKind.Crusher => new(64, 80), _ => Vector2.Zero
        };
        var item = new LevelObject(kind, new(Snap(p.X, 0, 30_000), Snap(p.Y, 0, 944), size.X, size.Y),
            kind is LevelObjectKind.Moving or LevelObjectKind.Enemy or LevelObjectKind.Hopper or LevelObjectKind.Hoverer or LevelObjectKind.Saw ? 64 : kind == LevelObjectKind.Crusher ? 160 : kind == LevelObjectKind.Conveyor ? 110 : 0);
        if (existing >= 0) { _objects[existing] = item; Selected = existing; }
        else { Selected = _objects.Count; _objects.Add(item); }
        Changed?.Invoke();
    }
    private static float Snap(float value, float min, float max) => Math.Clamp(MathF.Round(value / 16) * 16, min, max);
    public void BeginDrag(int index) { CancelDrag(); Select(index); if (Selected >= 0) _drag = Capture(); }
    public void MoveSelected(Vector2 delta)
    {
        if (_drag is null || Selected < 0 || !float.IsFinite(delta.X) || !float.IsFinite(delta.Y)) return;
        var original = _drag.Objects[Selected];
        _objects[Selected] = original with { Bounds = original.Bounds with { X = Snap(original.Bounds.X + delta.X, 0, 30_000), Y = Snap(original.Bounds.Y + delta.Y, 0, 944) } };
        Changed?.Invoke();
    }
    public void CommitDrag()
    {
        if (_drag is not { } start) return;
        _drag = null;
        if (!start.Objects.AsSpan().SequenceEqual(_objects.ToArray())) Remember(start);
        Changed?.Invoke();
    }
    public void CancelDrag() { if (_drag is { } state) { _drag = null; Restore(state); } }
    public void DeleteSelected()
    {
        CancelDrag(); if (Selected < 0) return;
        var state = Capture(); Remember(state);
        int selected = Selected, link = _objects[selected].PipeLink;
        if (link != 0) Restore(new(ClearLink(state.Rooms, link), RoomIndex));
        _objects.RemoveAt(selected); Selected = -1; Changed?.Invoke();
    }
    public void ResizeSelected(float delta)
    {
        CancelDrag(); if (Selected < 0) return;
        var item = _objects[Selected];
        if (item.Kind is not (LevelObjectKind.Ground or LevelObjectKind.Ledge or LevelObjectKind.Moving or LevelObjectKind.Stone or LevelObjectKind.Hazard or LevelObjectKind.Conveyor or LevelObjectKind.Ice or LevelObjectKind.Crumble)) return;
        Remember(Capture()); _objects[Selected] = item with { Bounds = item.Bounds with { Width = Snap(item.Bounds.Width + delta, 16, 2000) } }; Changed?.Invoke();
    }
    public void ReverseConveyor()
    {
        CancelDrag(); if (Selected < 0 || _objects[Selected].Kind != LevelObjectKind.Conveyor) return;
        Remember(Capture()); var item = _objects[Selected];
        _objects[Selected] = item with { Travel = item.Travel == 0 ? -110 : -item.Travel }; Changed?.Invoke();
    }
    public void Undo()
    {
        CancelDrag(); if (_undo.Count == 0) return;
        Revision++; _redo.Add(Capture()); var state = _undo[^1]; _undo.RemoveAt(_undo.Count - 1); Restore(state);
    }
    public void Redo()
    {
        CancelDrag(); if (_redo.Count == 0) return;
        Revision++; _undo.Add(Capture()); var state = _redo[^1]; _redo.RemoveAt(_redo.Count - 1); Restore(state);
    }
}
