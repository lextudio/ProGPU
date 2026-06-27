using System;
using System.Numerics;

#nullable enable
#pragma warning disable IDE0057, IDE0059, IDE0078, IDE0300, IDE0301, IDE0305

namespace ProGPU.Vector;

#if PROGPU_VECTOR_INTERNAL
internal
#else
public
#endif
static class BoundsHitTesting
{
    public static bool ContainsPoint(
        Vector2 point,
        Vector2 min,
        Vector2 max,
        float tolerance,
        bool relativeTolerance)
    {
        if (!IsFinite(point) || !IsFinite(min) || !IsFinite(max))
        {
            return false;
        }

        if (max.X < min.X || max.Y < min.Y)
        {
            return false;
        }

        if (float.IsFinite(tolerance) && tolerance > 0.0f)
        {
            float padding = tolerance;
            if (relativeTolerance)
            {
                padding *= MathF.Max(MathF.Abs(max.X - min.X), MathF.Abs(max.Y - min.Y));
            }

            if (float.IsFinite(padding) && padding > 0.0f)
            {
                var inflation = new Vector2(padding, padding);
                min -= inflation;
                max += inflation;
            }
        }

        return point.X >= min.X &&
               point.X <= max.X &&
               point.Y >= min.Y &&
               point.Y <= max.Y;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
