namespace ProGPU.GameEngine.Simulation;

public readonly record struct CharacterInput2D(double Move, bool JumpPressed, bool JumpHeld, bool Run, bool DropThrough = false);

/// <summary>Explicit caller-owned motion policy. These values do not identify a foreign game's physics.</summary>
public sealed record CharacterMotion2D(double WalkSpeed, double RunSpeed, double GroundAcceleration, double AirAcceleration,
    double Gravity, double MaximumFallSpeed, double JumpSpeed, double ReleasedJumpAcceleration, double ReleasedJumpThreshold,
    int CoyoteTicks, int JumpBufferTicks, int DropThroughTicks)
{
    public void Validate()
    {
        foreach (double value in new[] { WalkSpeed, RunSpeed, GroundAcceleration, AirAcceleration, Gravity, MaximumFallSpeed,
                     JumpSpeed, ReleasedJumpAcceleration, ReleasedJumpThreshold })
            if (!double.IsFinite(value) || value < 0) throw new ArgumentException("Character coefficients must be finite and nonnegative.");
        if (RunSpeed < WalkSpeed || Gravity == 0 || JumpSpeed == 0 || MaximumFallSpeed == 0 ||
            CoyoteTicks is < 0 or > 1200 || JumpBufferTicks is < 0 or > 1200 || DropThroughTicks is < 0 or > 1200)
            throw new ArgumentException("Invalid character motion policy or input-grace tick count.");
    }
}

/// <summary>
/// Fixed-step rectangle character driven by an explicit policy and immutable static
/// collision generation. No frame clock, rendering, parsing or allocation in Step.
/// Supports bounded buffered/coyote jumping, variable jump release and one-way
/// drop-through. Source coordinates remain double precision until the host renders.
/// No enemy, slope, moving-platform, rigid-body or foreign character algorithm is
/// implied by this component; callers implement those through their runtime rules.
/// </summary>
public sealed class KinematicCharacter2D
{
    private StaticCollisionWorld2D _world;
    private readonly CharacterMotion2D _motion;
    private int _coyote, _jumpBuffer, _dropThrough;
    public double SecondsPerTick { get; }
    public Bounds2D Bounds { get; private set; }
    public Bounds2D PreviousBounds { get; private set; }
    public double VelocityX { get; private set; }
    public double VelocityY { get; private set; }
    public bool Grounded { get; private set; }
    public int GroundCollider { get; private set; } = -1;
    public int Facing { get; private set; } = 1;
    public long Tick { get; private set; }
    public KinematicMove2D LastMove { get; private set; }

    public KinematicCharacter2D(StaticCollisionWorld2D world, Bounds2D spawn, CharacterMotion2D motion, double secondsPerTick)
    {
        ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(motion); motion.Validate();
        if (!double.IsFinite(secondsPerTick) || secondsPerTick <= 0 || secondsPerTick > .1) throw new ArgumentOutOfRangeException(nameof(secondsPerTick));
        _world = world; _motion = motion; SecondsPerTick = secondsPerTick; Teleport(world, spawn);
    }

    public void Teleport(StaticCollisionWorld2D world, Bounds2D target, bool preserveMomentum = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        var contact = world.Move(target, 0, 0);
        if (contact.OverlappingStart) throw new ArgumentException("The destination overlaps a solid collider.", nameof(target));
        _world = world; Bounds = PreviousBounds = target; Grounded = contact.Grounded; GroundCollider = contact.VerticalContact;
        LastMove = contact; _coyote = _jumpBuffer = _dropThrough = 0;
        if (!preserveMomentum) VelocityX = VelocityY = 0;
    }

    public void SetFacing(int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        Facing = direction;
    }

    public void Step(CharacterInput2D input)
    {
        PreviousBounds = Bounds; Tick++;
        _jumpBuffer = input.JumpPressed ? Math.Max(1, _motion.JumpBufferTicks) : Math.Max(0, _jumpBuffer - 1);
        _coyote = Grounded ? Math.Max(1, _motion.CoyoteTicks) : Math.Max(0, _coyote - 1);
        _dropThrough = input.DropThrough ? _motion.DropThroughTicks : Math.Max(0, _dropThrough - 1);
        if (_dropThrough > 0) { _coyote = _jumpBuffer = 0; Grounded = false; }
        double move = double.IsFinite(input.Move) ? Math.Clamp(input.Move, -1, 1) : 0;
        double target = move * (input.Run ? _motion.RunSpeed : _motion.WalkSpeed);
        double acceleration = (Grounded ? _motion.GroundAcceleration : _motion.AirAcceleration) * SecondsPerTick;
        VelocityX = VelocityX < target ? Math.Min(target, VelocityX + acceleration) : Math.Max(target, VelocityX - acceleration);
        if (Math.Abs(move) > .1) Facing = move > 0 ? 1 : -1;
        if (_coyote > 0 && _jumpBuffer > 0)
        { VelocityY = -_motion.JumpSpeed; Grounded = false; _coyote = _jumpBuffer = 0; }
        if (!input.JumpHeld && VelocityY < -_motion.ReleasedJumpThreshold)
            VelocityY += _motion.ReleasedJumpAcceleration * SecondsPerTick;
        VelocityY = Math.Min(_motion.MaximumFallSpeed, VelocityY + _motion.Gravity * SecondsPerTick);
        LastMove = _world.Move(Bounds, VelocityX * SecondsPerTick, VelocityY * SecondsPerTick, _dropThrough > 0);
        if (LastMove.OverlappingStart) throw new InvalidOperationException("Character started a tick inside a static solid; restore a valid runtime checkpoint.");
        Bounds = LastMove.Bounds; Grounded = LastMove.Grounded; GroundCollider = Grounded ? LastMove.VerticalContact : -1;
        if (LastMove.HorizontalContact >= 0) VelocityX = 0;
        if (LastMove.VerticalContact >= 0) VelocityY = 0;
    }
}
