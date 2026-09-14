namespace ProGPU.GameEngine.Simulation;

/// <summary>Fixed-tick warning/absence durations. No wall-clock or render-frame dependency.</summary>
public readonly struct TimedSupportPolicy
{
    public int WarningTicks { get; }
    public int AbsentTicks { get; }
    public long TotalTicks => (long)WarningTicks + AbsentTicks;
    public TimedSupportPolicy(int warningTicks, int absentTicks)
    {
        if (warningTicks <= 0 || absentTicks <= 0) throw new ArgumentOutOfRangeException(nameof(warningTicks));
        WarningTicks = warningTicks; AbsentTicks = absentTicks;
    }
}

/// <summary>
/// O(1) retained contact-triggered support state. Zero-initialized state is solid.
/// The scene owns one value per timed surface; querying never mutates the timer.
/// A new contact can rearm only after warning and absence have both elapsed.
/// Call Reset when restarting the scene clock. Ticks are nonnegative and monotonic.
/// </summary>
public struct TimedSupportState
{
    private long _triggerTick;
    private bool _triggered;

    public bool Touch(long tick, TimedSupportPolicy policy)
    {
        if (policy.TotalTicks == 0 || (_triggered && tick - _triggerTick < policy.TotalTicks)) return false;
        _triggerTick = tick; _triggered = true; return true;
    }
    public readonly bool IsSolid(long tick, TimedSupportPolicy policy)
    {
        if (!_triggered) return true;
        long age = tick - _triggerTick;
        return age < policy.WarningTicks || age >= policy.TotalTicks;
    }
    public readonly float WarningProgress(long tick, TimedSupportPolicy policy)
    {
        if (!_triggered || policy.WarningTicks == 0) return 0;
        long age = tick - _triggerTick;
        return age >= policy.TotalTicks ? 0 : Math.Clamp((float)age / policy.WarningTicks, 0, 1);
    }
    public void Reset() => this = default;
}
