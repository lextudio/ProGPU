using System;
using System.Numerics;

namespace ProGPU.Vector;

public static class ArcSegmentGeometry
{
    public const float DefaultFlattenAngleRadians = MathF.PI / 8.0f;
    public const int MaxFlattenSegmentCount = 64;

    private const float Epsilon = 0.00001f;

    public static bool TryGetArcCenter(
        Vector2 start,
        Vector2 end,
        Vector2 radii,
        float rotationAngleDegrees,
        bool isLargeArc,
        SweepDirection sweepDirection,
        out Vector2 center,
        out float theta1,
        out float deltaTheta,
        out float radiusX,
        out float radiusY)
    {
        center = default;
        theta1 = 0.0f;
        deltaTheta = 0.0f;
        radiusX = MathF.Abs(radii.X);
        radiusY = MathF.Abs(radii.Y);

        if (!IsFinite(start) ||
            !IsFinite(end) ||
            !IsFinite(radii) ||
            !float.IsFinite(rotationAngleDegrees) ||
            Vector2.DistanceSquared(start, end) <= Epsilon * Epsilon ||
            radiusX <= Epsilon ||
            radiusY <= Epsilon)
        {
            return false;
        }

        float phi = rotationAngleDegrees * MathF.PI / 180.0f;
        float cosPhi = MathF.Cos(phi);
        float sinPhi = MathF.Sin(phi);

        float dx = (start.X - end.X) * 0.5f;
        float dy = (start.Y - end.Y) * 0.5f;
        float x1p = cosPhi * dx + sinPhi * dy;
        float y1p = -sinPhi * dx + cosPhi * dy;

        float prx = radiusX * radiusX;
        float pry = radiusY * radiusY;
        float px1p = x1p * x1p;
        float py1p = y1p * y1p;

        float radiiCheck = px1p / prx + py1p / pry;
        if (!float.IsFinite(radiiCheck))
        {
            return false;
        }

        if (radiiCheck > 1.0f)
        {
            float scale = MathF.Sqrt(radiiCheck);
            radiusX *= scale;
            radiusY *= scale;
            prx = radiusX * radiusX;
            pry = radiusY * radiusY;
        }

        float denominator = prx * py1p + pry * px1p;
        if (denominator <= Epsilon || !float.IsFinite(denominator))
        {
            return false;
        }

        float sign = isLargeArc == (sweepDirection == SweepDirection.Clockwise) ? -1.0f : 1.0f;
        float sqTerm = (prx * pry - prx * py1p - pry * px1p) / denominator;
        if (!float.IsFinite(sqTerm))
        {
            return false;
        }

        if (sqTerm < 0.0f)
        {
            sqTerm = 0.0f;
        }

        float coef = sign * MathF.Sqrt(sqTerm);
        float cxp = coef * ((radiusX * y1p) / radiusY);
        float cyp = coef * -((radiusY * x1p) / radiusX);

        center = new Vector2(
            cosPhi * cxp - sinPhi * cyp + (start.X + end.X) * 0.5f,
            sinPhi * cxp + cosPhi * cyp + (start.Y + end.Y) * 0.5f);

        float ux = (x1p - cxp) / radiusX;
        float uy = (y1p - cyp) / radiusY;
        float vx = (-x1p - cxp) / radiusX;
        float vy = (-y1p - cyp) / radiusY;

        theta1 = MathF.Atan2(uy, ux);
        float theta2 = MathF.Atan2(vy, vx);

        deltaTheta = theta2 - theta1;
        if (sweepDirection == SweepDirection.Clockwise)
        {
            if (deltaTheta < 0.0f)
            {
                deltaTheta += 2.0f * MathF.PI;
            }
        }
        else if (deltaTheta > 0.0f)
        {
            deltaTheta -= 2.0f * MathF.PI;
        }

        return IsFinite(center) &&
               float.IsFinite(theta1) &&
               float.IsFinite(deltaTheta) &&
               float.IsFinite(radiusX) &&
               float.IsFinite(radiusY);
    }

    public static Vector2 EvaluatePoint(
        Vector2 center,
        float radiusX,
        float radiusY,
        float rotationAngleDegrees,
        float theta)
    {
        float phi = rotationAngleDegrees * MathF.PI / 180.0f;
        float cosPhi = MathF.Cos(phi);
        float sinPhi = MathF.Sin(phi);
        float cosTheta = MathF.Cos(theta);
        float sinTheta = MathF.Sin(theta);

        return new Vector2(
            radiusX * cosTheta * cosPhi - radiusY * sinTheta * sinPhi + center.X,
            radiusX * cosTheta * sinPhi + radiusY * sinTheta * cosPhi + center.Y);
    }

    public static int CountFlattenedSegments(
        Vector2 start,
        ArcSegment arc,
        float maxAngleRadians = DefaultFlattenAngleRadians)
    {
        if (!TryGetArcCenter(
                start,
                arc.Point,
                arc.Size,
                arc.RotationAngle,
                arc.IsLargeArc,
                arc.SweepDirection,
                out _,
                out _,
                out float deltaTheta,
                out _,
                out _))
        {
            return Vector2.DistanceSquared(start, arc.Point) > Epsilon * Epsilon ? 1 : 0;
        }

        return CountArcSegments(deltaTheta, maxAngleRadians);
    }

    public static Vector2[] FlattenArc(
        Vector2 start,
        ArcSegment arc,
        float maxAngleRadians = DefaultFlattenAngleRadians)
    {
        if (!TryGetArcCenter(
                start,
                arc.Point,
                arc.Size,
                arc.RotationAngle,
                arc.IsLargeArc,
                arc.SweepDirection,
                out var center,
                out float theta1,
                out float deltaTheta,
                out float radiusX,
                out float radiusY))
        {
            return Vector2.DistanceSquared(start, arc.Point) > Epsilon * Epsilon
                ? new[] { start, arc.Point }
                : new[] { start };
        }

        int segmentCount = CountArcSegments(deltaTheta, maxAngleRadians);
        var points = new Vector2[segmentCount + 1];
        points[0] = start;
        for (int i = 1; i < segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            points[i] = EvaluatePoint(center, radiusX, radiusY, arc.RotationAngle, theta1 + t * deltaTheta);
        }

        points[segmentCount] = arc.Point;
        return points;
    }

    private static int CountArcSegments(float deltaTheta, float maxAngleRadians)
    {
        if (!float.IsFinite(maxAngleRadians) || maxAngleRadians <= Epsilon)
        {
            maxAngleRadians = DefaultFlattenAngleRadians;
        }

        int segmentCount = (int)MathF.Ceiling(MathF.Abs(deltaTheta) / maxAngleRadians);
        return Math.Clamp(segmentCount, 1, MaxFlattenSegmentCount);
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
