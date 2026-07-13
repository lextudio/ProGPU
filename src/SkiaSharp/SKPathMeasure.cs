using System;
using System.Collections.Generic;
using System.Numerics;
using ProGPU.Vector;

namespace SkiaSharp;

[Flags]
public enum SKPathMeasureMatrixFlags
{
    GetPosition = 0x01,
    GetTangent = 0x02,
    GetPositionAndTangent = GetPosition | GetTangent
}

public sealed class SKPathMeasure : IDisposable
{
    private const float Epsilon = 0.00001f;

    private bool _forceClosed;
    private readonly int _curveSegmentCount;
    private readonly float _arcFlattenAngle;
    private SKPath? _path;
    private int _contourIndex;
    private Contour? _contour;

    public SKPathMeasure()
        : this(null, false, 1f)
    {
    }

    public SKPathMeasure(SKPath? path, bool forceClosed = false, float resScale = 1f)
    {
        _forceClosed = forceClosed;
        var normalizedScale = float.IsFinite(resScale) && resScale > 0f ? resScale : 1f;
        _curveSegmentCount = Math.Clamp((int)MathF.Ceiling(32f * normalizedScale), 8, 256);
        _arcFlattenAngle = MathF.PI / Math.Clamp(16f * normalizedScale, 8f, 128f);
        SetPath(path, forceClosed);
    }

    public float Length => _contour?.Length ?? 0f;

    public bool IsClosed => _contour?.IsClosed ?? false;

    public void SetPath(SKPath? path) => SetPath(path, forceClosed: false);

    public void SetPath(SKPath? path, bool forceClosed)
    {
        _path?.Dispose();
        _path = path == null ? null : new SKPath(path);
        _forceClosed = forceClosed;
        _contourIndex = 0;
        _contour = BuildContour(_contourIndex, forceClosed);
    }

    public bool NextContour()
    {
        if (_path == null || _contourIndex + 1 >= _path.Geometry.Figures.Count)
        {
            return false;
        }

        _contourIndex++;
        _contour = BuildContour(_contourIndex, _forceClosed);
        return true;
    }

    public bool GetPosition(float distance, out SKPoint position)
    {
        if (!TryGetSample(distance, out var point, out _))
        {
            position = default;
            return false;
        }

        position = new SKPoint(point.X, point.Y);
        return true;
    }

    public SKPoint GetPosition(float distance) =>
        GetPosition(distance, out var position) ? position : SKPoint.Empty;

    public bool GetTangent(float distance, out SKPoint tangent)
    {
        if (!TryGetSample(distance, out _, out var direction))
        {
            tangent = default;
            return false;
        }

        tangent = new SKPoint(direction.X, direction.Y);
        return true;
    }

    public SKPoint GetTangent(float distance) =>
        GetTangent(distance, out var tangent) ? tangent : SKPoint.Empty;

    public bool GetPositionAndTangent(float distance, out SKPoint position, out SKPoint tangent)
    {
        if (!TryGetSample(distance, out var point, out var direction))
        {
            position = default;
            tangent = default;
            return false;
        }

        position = new SKPoint(point.X, point.Y);
        tangent = new SKPoint(direction.X, direction.Y);
        return true;
    }

    public bool GetMatrix(
        float distance,
        out SKMatrix matrix,
        SKPathMeasureMatrixFlags flags)
    {
        if (!TryGetSample(distance, out var point, out var tangent))
        {
            matrix = SKMatrix.Identity;
            return false;
        }

        matrix = SKMatrix.Identity;
        if ((flags & SKPathMeasureMatrixFlags.GetTangent) != 0)
        {
            matrix.ScaleX = tangent.X;
            matrix.SkewX = -tangent.Y;
            matrix.SkewY = tangent.Y;
            matrix.ScaleY = tangent.X;
        }

        if ((flags & SKPathMeasureMatrixFlags.GetPosition) != 0)
        {
            matrix.TransX = point.X;
            matrix.TransY = point.Y;
        }

        return true;
    }

    public SKMatrix GetMatrix(float distance, SKPathMeasureMatrixFlags flags) =>
        GetMatrix(distance, out var matrix, flags) ? matrix : SKMatrix.Empty;

    [Obsolete("Use the SKPathBuilder overload instead.")]
    public bool GetSegment(
        float start,
        float stop,
        SKPath dst,
        bool startWithMoveTo)
    {
        ArgumentNullException.ThrowIfNull(dst);
        using var builder = new SKPathBuilder();
        if (!GetSegment(start, stop, builder, startWithMoveTo))
        {
            return false;
        }

        using var segment = builder.Detach();
        dst.Reset();
        dst.AddPath(segment);
        return true;
    }

    public bool GetSegment(
        float start,
        float stop,
        SKPathBuilder dst,
        bool startWithMoveTo)
    {
        ArgumentNullException.ThrowIfNull(dst);
        var contour = _contour;
        if (contour == null || contour.Length <= Epsilon || !float.IsFinite(start) || !float.IsFinite(stop))
        {
            return false;
        }

        var clampedStart = Math.Clamp(start, 0f, contour.Length);
        var clampedStop = Math.Clamp(stop, 0f, contour.Length);
        if (clampedStop <= clampedStart + Epsilon)
        {
            return false;
        }

        var startPoint = contour.GetPoint(clampedStart);
        if (startWithMoveTo || dst.IsEmpty)
        {
            dst.MoveTo(startPoint.X, startPoint.Y);
        }
        else
        {
            dst.LineTo(startPoint.X, startPoint.Y);
        }

        for (var i = 1; i < contour.Samples.Count - 1; i++)
        {
            var sample = contour.Samples[i];
            if (sample.Distance > clampedStart + Epsilon && sample.Distance < clampedStop - Epsilon)
            {
                dst.LineTo(sample.Point.X, sample.Point.Y);
            }
        }

        var stopPoint = contour.GetPoint(clampedStop);
        dst.LineTo(stopPoint.X, stopPoint.Y);
        return true;
    }

    public SKPath? GetSegment(float start, float stop, bool startWithMoveTo)
    {
        using var builder = new SKPathBuilder();
        return GetSegment(start, stop, builder, startWithMoveTo)
            ? builder.Detach()
            : null;
    }

    private bool TryGetSample(float distance, out Vector2 point, out Vector2 tangent)
    {
        var contour = _contour;
        if (contour == null || contour.Length <= Epsilon || !float.IsFinite(distance))
        {
            point = default;
            tangent = default;
            return false;
        }

        var normalizedDistance = Math.Clamp(distance, 0f, contour.Length);
        point = contour.GetPoint(normalizedDistance);
        tangent = contour.GetTangent(normalizedDistance);
        return tangent.LengthSquared() > Epsilon * Epsilon;
    }

    private Contour? BuildContour(int index, bool forceClosed)
    {
        if (_path == null || index < 0 || index >= _path.Geometry.Figures.Count)
        {
            return null;
        }

        var figure = _path.Geometry.Figures[index];
        var contour = new Contour(figure.IsClosed || forceClosed);
        var current = figure.StartPoint;
        contour.Add(current);

        foreach (var segment in figure.Segments)
        {
            switch (segment)
            {
                case LineSegment line:
                    contour.Add(line.Point);
                    current = line.Point;
                    break;

                case QuadraticBezierSegment quadratic:
                    for (var step = 1; step <= _curveSegmentCount; step++)
                    {
                        var t = (float)step / _curveSegmentCount;
                        contour.Add(BezierSegmentGeometry.EvaluateQuadratic(
                            current,
                            quadratic.ControlPoint,
                            quadratic.Point,
                            t));
                    }

                    current = quadratic.Point;
                    break;

                case CubicBezierSegment cubic:
                    for (var step = 1; step <= _curveSegmentCount; step++)
                    {
                        var t = (float)step / _curveSegmentCount;
                        contour.Add(BezierSegmentGeometry.EvaluateCubic(
                            current,
                            cubic.ControlPoint1,
                            cubic.ControlPoint2,
                            cubic.Point,
                            t));
                    }

                    current = cubic.Point;
                    break;

                case ArcSegment arc:
                    var points = ArcSegmentGeometry.FlattenArc(current, arc, _arcFlattenAngle);
                    for (var pointIndex = 1; pointIndex < points.Length; pointIndex++)
                    {
                        contour.Add(points[pointIndex]);
                    }

                    current = arc.Point;
                    break;
            }
        }

        if (contour.IsClosed)
        {
            contour.Add(figure.StartPoint);
        }

        return contour;
    }

    public void Dispose()
    {
        _path?.Dispose();
        _path = null;
        _contour = null;
    }

    private readonly record struct Sample(Vector2 Point, float Distance);

    private sealed class Contour
    {
        public Contour(bool isClosed)
        {
            IsClosed = isClosed;
        }

        public List<Sample> Samples { get; } = new();

        public bool IsClosed { get; }

        public float Length => Samples.Count == 0 ? 0f : Samples[^1].Distance;

        public void Add(Vector2 point)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
            {
                return;
            }

            if (Samples.Count == 0)
            {
                Samples.Add(new Sample(point, 0f));
                return;
            }

            var previous = Samples[^1];
            var segmentLength = Vector2.Distance(previous.Point, point);
            if (segmentLength <= Epsilon)
            {
                return;
            }

            Samples.Add(new Sample(point, previous.Distance + segmentLength));
        }

        public Vector2 GetPoint(float distance)
        {
            var segmentIndex = FindSegment(distance);
            var start = Samples[segmentIndex - 1];
            var end = Samples[segmentIndex];
            var segmentLength = end.Distance - start.Distance;
            var amount = segmentLength <= Epsilon ? 0f : (distance - start.Distance) / segmentLength;
            return Vector2.Lerp(start.Point, end.Point, Math.Clamp(amount, 0f, 1f));
        }

        public Vector2 GetTangent(float distance)
        {
            var segmentIndex = FindSegment(distance);
            var direction = Samples[segmentIndex].Point - Samples[segmentIndex - 1].Point;
            return direction.LengthSquared() <= Epsilon * Epsilon
                ? Vector2.Zero
                : Vector2.Normalize(direction);
        }

        private int FindSegment(float distance)
        {
            if (Samples.Count < 2)
            {
                return 0;
            }

            if (distance <= 0f)
            {
                return 1;
            }

            if (distance >= Length)
            {
                return Samples.Count - 1;
            }

            var low = 1;
            var high = Samples.Count - 1;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (Samples[middle].Distance < distance)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }
    }
}
