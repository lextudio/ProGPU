using System.Runtime.InteropServices;
using ProGPU.GameEngine.Simulation;

namespace ProGPU.Samples.Suntrail.Game.Import;

public readonly record struct SmbxPlaytestInput(double Move, bool JumpHeld, bool JumpPressed, bool Run, bool UsePressed);
public readonly record struct SmbxPlaytestSection(int Record, Bounds2D Bounds);
public readonly record struct SmbxPlaytestWarp(int Record, Bounds2D Entrance, Bounds2D Destination, int Section,
    bool Automatic, bool PreserveMomentum, bool RequiresGround);

/// <summary>
/// Explicit geometry playtest policy, not an SMBX engine emulation. Source blocks
/// become static rectangular solids, and the original Suntrail courier motion is
/// used. Unknown IDs never acquire guessed NPC algorithms. All source bytes remain
/// in the immutable editor snapshot. Local, unlocked warps are projected using a
/// documented 32-unit aperture policy; scripts, NPCs and physics zones stay inert.
/// Preparation is O(N log N + W*S), bounded by 65,536 source records, 4,096 warp
/// records and 256 sections. Tick work is collision-query cost plus O(W + S), with
/// no source parsing, image decoding, mutable editor access or normal allocation.
/// </summary>
public sealed class SmbxPlaytest
{
    public const double StepSeconds = 1d / 120;
    // Original in-repository provenance: GameSession.Step/MoveAndCollide. These
    // coefficients are Suntrail's controls; they are not Mario calibration data.
    private static readonly CharacterMotion2D Motion = new(300, 390, 2300, 1400, 1760, 980, 720, 2600, 270, 14, 16, 24);
    private readonly SmbxPlaytestSection[] _sections;
    private readonly SmbxPlaytestWarp[] _warps;
    private readonly SmbxGeometryIssue[] _issues;
    private readonly Bounds2D _spawn;
    private readonly int _spawnSection;
    private readonly int _spawnFacing;
    private double _accumulator;
    private bool _jumpQueued, _useQueued;
    private int _warpCooldown;
    internal SmbxGeometryEditor Snapshot { get; }
    public SmbxSourceDocument Document => Snapshot.Document;
    public StaticCollisionWorld2D Collision { get; }
    public KinematicCharacter2D Character { get; }
    public ReadOnlySpan<SmbxPlaytestSection> Sections => _sections;
    public ReadOnlySpan<SmbxPlaytestWarp> Warps => _warps;
    public ReadOnlySpan<SmbxGeometryIssue> Issues => _issues;
    public int IssueCount { get; }
    public int SectionIndex { get; private set; }
    public bool Paused { get; private set; }
    public int Falls { get; private set; }
    public int Transfers { get; private set; }
    public uint Revision { get; private set; }
    public string? Notice { get; private set; }

    public SmbxPlaytest(SmbxSourceDocument document, int preferredPlayerRecord = -1)
    {
        Snapshot = new(document);
        var sections = new List<SmbxPlaytestSection>(); var solids = new List<StaticCollider2D>();
        var issues = new List<SmbxGeometryIssue>(); int issueCount = 0;
        void Issue(int record, string message) { issueCount++; if (issues.Count < 128) issues.Add(new(record, message)); }
        foreach (var issue in Snapshot.Issues) Issue(issue.Record, issue.Message);
        SmbxGeometry? firstPlayer = null, playerOne = null, preferred = null;
        foreach (var item in Snapshot.Geometry)
        {
            var b = item.Bounds;
            if (item.Kind == SmbxGeometryKind.Section)
            {
                if (sections.Count == 256) throw new FormatException("Geometry playtest supports at most 256 sections.");
                sections.Add(new(item.Record, new(b.X, b.Y, b.Width, b.Height)));
            }
            else if (item.Kind == SmbxGeometryKind.Block) solids.Add(new(item.Record, new(b.X, b.Y, b.Width, b.Height)));
            else if (item.Kind == SmbxGeometryKind.PlayerAnchor)
            {
                firstPlayer ??= item;
                if (item.Record == preferredPlayerRecord) preferred = item;
                if (item.SourceId == "1") playerOne ??= item;
            }
            else if (item.Kind is SmbxGeometryKind.NpcAnchor or SmbxGeometryKind.Physics)
                Issue(item.Record, "Geometry playtest preserves this object as scenery; its gameplay behavior is not simulated.");
        }
        if (sections.Count == 0) throw new FormatException("Add a valid section before starting a geometry playtest.");
        var spawn = preferred ?? playerOne ?? firstPlayer ?? throw new FormatException("Add a player start before starting a geometry playtest.");
        _sections = sections.ToArray(); Collision = new(CollectionsMarshal.AsSpan(solids));
        _spawn = new(spawn.Bounds.X, spawn.Bounds.Y, 30, 48);
        _spawnSection = FindSection(_spawn);
        if (_spawnSection < 0) throw new FormatException("The Suntrail courier's 30 × 48 start rectangle must fit inside a section.");
        var spawnRow = document.Records[spawn.Record];
        int direction = Integer(spawnRow, "D", 0);
        _spawnFacing = direction < 0 ? -1 : direction > 0 ? 1 :
            _spawn.X < _sections[_spawnSection].Bounds.X + _sections[_spawnSection].Bounds.Width / 2 ? 1 : -1;
        if (Collision.Move(_spawn, 0, 0).OverlappingStart)
            throw new FormatException("Move the player start clear of solid blocks before playtesting (courier size: 30 × 48).");
        Character = new(Collision, _spawn, Motion, StepSeconds); Character.SetFacing(_spawnFacing);
        SectionIndex = _spawnSection;
        var warps = new List<SmbxPlaytestWarp>(); int warpRecords = 0;
        for (int record = 0; record < document.Records.Length; record++)
        {
            var row = document.Records[record]; if (row.SectionPath != row.Section) continue;
            if (row.Section is "LAYERS" or "EVENTS_CLASSIC" or "EVENTS")
            { Issue(record, "Layer visibility, movement and events remain inactive in geometry playtest."); continue; }
            if (row.Section != "DOORS") continue;
            if (++warpRecords > 4096) throw new FormatException("Geometry playtest supports at most 4,096 warp records.");
            try
            {
                if (Text(row, "LF").Length != 0 || Boolean(row, "ET") || Boolean(row, "EX") ||
                    Integer(row, "WX", -1) != -1 || Integer(row, "WY", -1) != -1)
                    throw new FormatException("External-level, world-map and entrance/exit-only warps need episode runtime support.");
                if (Integer(row, "SL", 0) != 0 || Boolean(row, "LC") || Boolean(row, "LB") || Boolean(row, "SR") ||
                    Boolean(row, "PT") || Text(row, "EE").Length != 0 || Text(row, "EEX").Length != 0)
                    throw new FormatException("This warp has a requirement, event or cannon rule that geometry playtest does not simulate.");
                int type = Integer(row, "DT", 1), entryDirection = Integer(row, "ID", 3), exitDirection = Integer(row, "OD", 3);
                if (type is < 0 or > 3 || entryDirection is < 1 or > 4 || exitDirection is < 1 or > 4)
                    throw new FormatException("This warp has an unsupported type or direction.");
                int entryLength = Integer(row, "IL", 32), exitLength = Integer(row, "OL", 32);
                if (entryLength is < 1 or > 8192 || exitLength is < 1 or > 8192)
                    throw new FormatException("Playtest warp aperture lengths must be from 1 through 8192.");
                var entry = Aperture(row.Get("IX").GetNumber(), row.Get("IY").GetNumber(), entryDirection, entryLength);
                var exit = Aperture(row.Get("OX").GetNumber(), row.Get("OY").GetNumber(), exitDirection, exitLength);
                AddWarp(entry, exit, exitDirection);
                if (Boolean(row, "TW")) AddWarp(exit, entry, entryDirection);

                void AddWarp(Bounds2D from, Bounds2D to, int outgoing)
                {
                    // OD and reverse-ID use the same *outward* numeric directions:
                    // 1 down, 2 right, 3 up, 4 left. Source entry directions are inward.
                    var target = type is 0 or 3 ? new Bounds2D(to.X + (to.Width - 30) / 2, to.Y + to.Height - 48, 30, 48) : outgoing switch
                    {
                        1 => new(to.X + (to.Width - 30) / 2, to.Bottom, 30, 48),
                        2 => new(to.Right, to.Y + (to.Height - 48) / 2, 30, 48),
                        3 => new(to.X + (to.Width - 30) / 2, to.Y - 48, 30, 48),
                        _ => new(to.X - 30, to.Y + (to.Height - 48) / 2, 30, 48)
                    };
                    int section = FindSection(target);
                    if (section < 0 || Collision.Move(target, 0, 0).OverlappingStart)
                        throw new FormatException("The playtest warp destination does not fit clear of blocks inside a section.");
                    warps.Add(new(record, from, target, section, type is 0 or 3, type == 3, Boolean(row, "STR")));
                }
            }
            catch (Exception error) when (error is FormatException or ArgumentException)
            {
                // A two-way record is atomic: a rejected reverse endpoint must not
                // leave an apparently supported one-way connection behind.
                while (warps.Count > 0 && warps[^1].Record == record) warps.RemoveAt(warps.Count - 1);
                Issue(record, error.Message);
            }
        }
        _warps = warps.ToArray(); _issues = issues.ToArray(); IssueCount = issueCount;
    }

    private static int Integer(SmbxSourceRecord row, string field, int fallback) => row.TryGet(field, out var value) ? value.GetInteger() : fallback;
    private static bool Boolean(SmbxSourceRecord row, string field) => row.TryGet(field, out var value) && value.GetBoolean();
    private static string Text(SmbxSourceRecord row, string field) => row.TryGet(field, out var value) ? value.GetString() : "";
    private static Bounds2D Aperture(double x, double y, int direction, int length)
    {
        var bounds = new Bounds2D(x, y, direction is 1 or 3 ? length : 32, direction is 2 or 4 ? length : 32);
        if (!bounds.IsValid || Math.Abs(x) > SmbxGeometryEditor.CoordinateLimit || Math.Abs(y) > SmbxGeometryEditor.CoordinateLimit)
            throw new FormatException("Warp coordinates exceed the geometry playtest range.");
        return bounds;
    }
    private int FindSection(Bounds2D body)
    {
        for (int i = 0; i < _sections.Length; i++)
        {
            var b = _sections[i].Bounds;
            if (body.X >= b.X && body.Y >= b.Y && body.Right <= b.Right && body.Bottom <= b.Bottom) return i;
        }
        return -1;
    }
    public void SetPaused(bool paused)
    { if (Paused == paused) return; Paused = paused; ClearPendingInput(); Revision++; }
    public void ClearPendingInput() { _accumulator = 0; _jumpQueued = _useQueued = false; }
    public void Restart()
    {
        Character.Teleport(Collision, _spawn); Character.SetFacing(_spawnFacing); SectionIndex = _spawnSection;
        _warpCooldown = 60; Notice = null; ClearPendingInput(); Revision++;
    }
    public void Advance(double elapsedSeconds, SmbxPlaytestInput input)
    {
        if (Paused) { ClearPendingInput(); return; }
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0) return;
        _jumpQueued |= input.JumpPressed; _useQueued |= input.UsePressed;
        _accumulator = Math.Min(_accumulator + elapsedSeconds, StepSeconds * 12);
        while (_accumulator >= StepSeconds)
        {
            _accumulator -= StepSeconds;
            Character.Step(new(input.Move, _jumpQueued, input.JumpHeld, input.Run)); _jumpQueued = false;
            if (_warpCooldown > 0) _warpCooldown--;
            bool transferred = false;
            if (_warpCooldown == 0)
                foreach (var warp in _warps)
                {
                    var trigger = warp.Entrance with { X = warp.Entrance.X - 2, Y = warp.Entrance.Y - 2,
                        Width = warp.Entrance.Width + 4, Height = warp.Entrance.Height + 4 };
                    if ((!warp.Automatic && !_useQueued) || (warp.RequiresGround && !Character.Grounded) || !Character.Bounds.Intersects(trigger)) continue;
                    Character.Teleport(Collision, warp.Destination, warp.PreserveMomentum);
                    SectionIndex = warp.Section; _warpCooldown = 60; Transfers++; transferred = true; break;
                }
            _useQueued = false;
            if (!transferred)
            {
                var section = _sections[SectionIndex].Bounds; var body = Character.Bounds;
                int next = FindSection(body); if (next >= 0) SectionIndex = next;
                else if (body.Y > section.Bottom + 96 || body.Right < section.X - 96 || body.X > section.Right + 96)
                { Falls++; Restart(); Notice = "Returned to the player start."; break; }
            }
            Revision++;
        }
    }
}
