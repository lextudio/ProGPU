namespace ProGPU.Samples.Suntrail.Game;

/// <summary>Deterministic input-only traversal driver used by gameplay and rendering verification.</summary>
public static class RoutePilot
{
    public static GameInput GetInput(GameSession game)
    {
        bool jump = false;
        float foot = game.Position.Y + GameSession.PlayerHeight;
        float front = game.Position.X + GameSession.PlayerWidth;
        if (!game.Grounded)
        {
            bool safeLanding = false;
            for (int platformIndex = 0; platformIndex < game.Level.Platforms.Length; platformIndex++)
            {
                var p = game.Level.Platforms[platformIndex];
                if (!game.Level.IsPlatformSolid(platformIndex, game.Tick)) continue;
                var b = p.At(game.Time);
                float discriminant = game.Velocity.Y * game.Velocity.Y + 3520 * (b.Y - foot);
                if (discriminant < 0) continue;
                float t = (-game.Velocity.Y + System.MathF.Sqrt(discriminant)) / 1760;
                if (t <= 0 || t > 2) continue;
                // Moving support positions and timed collapse are observable level
                // state. Predict them without moving the player or touching support
                // state; the pilot still supplies only ordinary input commands.
                b = p.At(game.Time + t);
                discriminant = game.Velocity.Y * game.Velocity.Y + 3520 * (b.Y - foot);
                if (discriminant < 0) continue;
                t = (-game.Velocity.Y + System.MathF.Sqrt(discriminant)) / 1760;
                if (t <= 0 || t > 2 || !game.Level.IsPlatformSolid(platformIndex, game.Tick + (long)(t / GameSession.StepSeconds))) continue;
                b = p.At(game.Time + t);
                float ramp = System.Math.Max(0, 300 - game.Velocity.X) / 1400;
                float accelerated = System.Math.Min(t, ramp);
                float distance = game.Velocity.X * accelerated + 700 * accelerated * accelerated + 300 * System.Math.Max(0, t - ramp);
                float landingX = game.Position.X + distance;
                if (landingX >= b.X + 8 && landingX + GameSession.PlayerWidth <= b.Right - 12) safeLanding = true;
            }
            // Brake an enemy hop whose ballistic landing would be in a gap. This
            // uses the same movement keys as a player; it never changes world state.
            if (!safeLanding) return new(0, true, false, false);
        }
        if (game.Grounded)
        {
            bool floorAhead = false;
            for (int i = 0; i < game.Level.Platforms.Length; i++)
            {
                if (!game.Level.IsPlatformSolid(i, game.Tick)) continue;
                var p = game.Level.Platforms[i]; var b = p.At(game.Time);
                if (front + 42 >= b.X && front + 42 < b.Right && System.Math.Abs(b.Y - foot) < 5) floorAhead = true;
                if (p.Kind is PlatformKind.Crate or PlatformKind.Pipe or PlatformKind.Stone or PlatformKind.Spring or PlatformKind.Conveyor or PlatformKind.Ice && b.X >= front - 8 && b.X < front + 95 && foot > b.Y) jump = true;
            }
            jump |= !floorAhead;
            foreach (var e in game.Level.Enemies)
                if (!e.Defeated && e.Position.X > front - 5 && e.Position.X < front + 110 && System.Math.Abs(e.Position.Y + 34 - foot) < 35) jump = true;
            foreach (var h in game.Level.Hazards)
                if (h.X > front - 5 && h.X < front + 95 && System.Math.Abs(h.Bottom - foot) < 35) jump = true;
            foreach (var mechanism in game.Level.Mechanisms)
            {
                var h = mechanism.At(game.Time);
                if (h.Right > game.Position.X && h.X < front + 110 && System.Math.Abs(h.Bottom - foot) < 40)
                    jump = true;
            }
            if (jump)
            {
                // Wait for a clear launch window beneath moving gallery hazards.
                // Predict the ordinary held-jump arc; this never changes collision.
                foreach (var mechanism in game.Level.Mechanisms)
                {
                    for (int sample = 1; sample <= 5; sample++)
                    {
                        float t = sample * .1f;
                        var arc = new Box(game.Position.X + 300 * t,
                            game.Position.Y - 720 * t + 880 * t * t,
                            GameSession.PlayerWidth, GameSession.PlayerHeight);
                        if (mechanism.IsDangerous(game.Time + t) && arc.Intersects(mechanism.At(game.Time + t)))
                            return new(0, true, false, false);
                    }
                }
            }
        }
        return new(1, true, jump, false);
    }
}
