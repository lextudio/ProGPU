namespace ProGPU.GameEngine.Simulation;

/// <summary>
/// Kinematic character response along a contact tangent. The caller supplies its
/// tangent/normal basis, collision detection and fixed step. O(1), allocation-free;
/// usable on a 2D axis or a tangent of a 3D contact plane. This is not a rigid-body solver.
/// </summary>
public readonly struct ContactSurface
{
    public float DriveAcceleration { get; }
    public float BrakeAcceleration { get; }
    public float TangentSpeed { get; }
    public float LaunchSpeed { get; }

    public ContactSurface(float driveAcceleration, float brakeAcceleration, float tangentSpeed = 0, float launchSpeed = 0)
    {
        if (!float.IsFinite(driveAcceleration) || driveAcceleration < 0 ||
            !float.IsFinite(brakeAcceleration) || brakeAcceleration < 0 ||
            !float.IsFinite(tangentSpeed) || !float.IsFinite(launchSpeed) || launchSpeed < 0)
            throw new ArgumentOutOfRangeException(nameof(driveAcceleration), "Surface coefficients must be finite; accelerations and launch speed must be nonnegative.");
        DriveAcceleration = driveAcceleration; BrakeAcceleration = brakeAcceleration;
        TangentSpeed = tangentSpeed; LaunchSpeed = launchSpeed;
    }

    /// <summary>Finite velocities and a finite nonnegative fixed step are caller preconditions.</summary>
    public float IntegrateTangent(float currentVelocity, float desiredRelativeVelocity, float seconds)
    {
        float target = desiredRelativeVelocity + TangentSpeed;
        float acceleration = desiredRelativeVelocity == 0 ? BrakeAcceleration : DriveAcceleration;
        float delta = acceleration * seconds;
        return currentVelocity < target ? Math.Min(currentVelocity + delta, target) : Math.Max(currentVelocity - delta, target);
    }
}
