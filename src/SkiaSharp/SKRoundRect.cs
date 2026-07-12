using System;

namespace SkiaSharp;

public enum SKRoundRectCorner
{
    UpperLeft,
    UpperRight,
    LowerRight,
    LowerLeft,
}

public enum SKRoundRectType
{
    Empty,
    Rect,
    Oval,
    Simple,
    NinePatch,
    Complex,
}

public class SKRoundRect : SKObject, ISKSkipObjectRegistration
{
    private const float DefaultCircularTolerance = 1f / 4096f;

    private readonly SKPoint[] _radii = new SKPoint[4];
    private SKRect _rect;
    private SKRoundRectType _type;

    public SKRect Rect => _rect;

    public SKPoint[] Radii =>
    [
        _radii[(int)SKRoundRectCorner.UpperLeft],
        _radii[(int)SKRoundRectCorner.UpperRight],
        _radii[(int)SKRoundRectCorner.LowerRight],
        _radii[(int)SKRoundRectCorner.LowerLeft],
    ];

    public SKRoundRectType Type => _type;

    public float Width => _rect.Width;

    public float Height => _rect.Height;

    public bool IsValid => Validate();

    public bool AllCornersCircular => CheckAllCornersCircular(DefaultCircularTolerance);

    internal ReadOnlySpan<SKPoint> CornerRadii => _radii;

    public SKRoundRect()
        : base(SKObjectHandle.Create(), owns: true)
    {
        SetEmpty();
    }

    public SKRoundRect(SKRect rect)
        : base(SKObjectHandle.Create(), owns: true)
    {
        SetRect(rect);
    }

    public SKRoundRect(SKRect rect, float radius)
        : this(rect, radius, radius)
    {
    }

    public SKRoundRect(SKRect rect, float xRadius, float yRadius)
        : base(SKObjectHandle.Create(), owns: true)
    {
        SetRect(rect, xRadius, yRadius);
    }

    public SKRoundRect(SKRoundRect rrect)
        : base(SKObjectHandle.Create(), owns: true)
    {
        CopyFrom(rrect);
    }

    public bool CheckAllCornersCircular(float tolerance) =>
        NearlyEqual(_radii[0].X, _radii[0].Y, tolerance) &&
        NearlyEqual(_radii[1].X, _radii[1].Y, tolerance) &&
        NearlyEqual(_radii[2].X, _radii[2].Y, tolerance) &&
        NearlyEqual(_radii[3].X, _radii[3].Y, tolerance);

    public void SetEmpty()
    {
        _rect = SKRect.Empty;
        Array.Clear(_radii);
        _type = SKRoundRectType.Empty;
    }

    public void SetRect(SKRect rect)
    {
        if (!InitializeRect(rect))
        {
            return;
        }

        Array.Clear(_radii);
        _type = SKRoundRectType.Rect;
    }

    public void SetRect(SKRect rect, float xRadius, float yRadius)
    {
        if (!InitializeRect(rect))
        {
            return;
        }

        if (!float.IsFinite(xRadius) || !float.IsFinite(yRadius))
        {
            SetRect(rect);
            return;
        }

        if (_rect.Width < xRadius + xRadius || _rect.Height < yRadius + yRadius)
        {
            var scale = MathF.Min(_rect.Width / (xRadius + xRadius), _rect.Height / (yRadius + yRadius));
            xRadius *= scale;
            yRadius *= scale;
        }

        if (xRadius <= 0f || yRadius <= 0f)
        {
            SetRect(rect);
            return;
        }

        Array.Fill(_radii, new SKPoint(xRadius, yRadius));
        _type = xRadius >= _rect.Width * 0.5f && yRadius >= _rect.Height * 0.5f
            ? SKRoundRectType.Oval
            : SKRoundRectType.Simple;
    }

    public void SetOval(SKRect rect)
    {
        if (!InitializeRect(rect))
        {
            return;
        }

        var xRadius = _rect.Width * 0.5f;
        var yRadius = _rect.Height * 0.5f;
        if (xRadius == 0f || yRadius == 0f)
        {
            Array.Clear(_radii);
            _type = SKRoundRectType.Rect;
            return;
        }

        Array.Fill(_radii, new SKPoint(xRadius, yRadius));
        _type = SKRoundRectType.Oval;
    }

    public void SetNinePatch(
        SKRect rect,
        float leftRadius,
        float topRadius,
        float rightRadius,
        float bottomRadius)
    {
        if (!InitializeRect(rect))
        {
            return;
        }

        if (!float.IsFinite(leftRadius) || !float.IsFinite(topRadius) ||
            !float.IsFinite(rightRadius) || !float.IsFinite(bottomRadius))
        {
            SetRect(rect);
            return;
        }

        leftRadius = MathF.Max(leftRadius, 0f);
        topRadius = MathF.Max(topRadius, 0f);
        rightRadius = MathF.Max(rightRadius, 0f);
        bottomRadius = MathF.Max(bottomRadius, 0f);
        var scale = 1f;
        if (leftRadius + rightRadius > _rect.Width)
        {
            scale = _rect.Width / (leftRadius + rightRadius);
        }

        if (topRadius + bottomRadius > _rect.Height)
        {
            scale = MathF.Min(scale, _rect.Height / (topRadius + bottomRadius));
        }

        if (scale < 1f)
        {
            leftRadius *= scale;
            topRadius *= scale;
            rightRadius *= scale;
            bottomRadius *= scale;
        }

        _radii[0] = new SKPoint(leftRadius, topRadius);
        _radii[1] = new SKPoint(rightRadius, topRadius);
        _radii[2] = new SKPoint(rightRadius, bottomRadius);
        _radii[3] = new SKPoint(leftRadius, bottomRadius);
        ClampSquareCorners();
        ComputeType();
    }

    public void SetRectRadii(SKRect rect, SKPoint[] radii)
    {
        ArgumentNullException.ThrowIfNull(radii);
        SetRectRadii(rect, radii.AsSpan());
    }

    public void SetRectRadii(SKRect rect, ReadOnlySpan<SKPoint> radii)
    {
        if (radii.Length != 4)
        {
            throw new ArgumentException("Radii must have a length of 4.", nameof(radii));
        }

        if (!InitializeRect(rect))
        {
            return;
        }

        for (var index = 0; index < radii.Length; index++)
        {
            if (!float.IsFinite(radii[index].X) || !float.IsFinite(radii[index].Y))
            {
                SetRect(rect);
                return;
            }

            _radii[index] = radii[index];
        }

        ClampSquareCorners();
        if (_radii[0] == default && _radii[1] == default &&
            _radii[2] == default && _radii[3] == default)
        {
            SetRect(rect);
            return;
        }

        ScaleRadii();
    }

    public bool Contains(SKRect rect)
    {
        if (!_rect.Contains(rect))
        {
            return false;
        }

        if (_type == SKRoundRectType.Rect)
        {
            return true;
        }

        return CheckCornerContainment(rect.Left, rect.Top) &&
               CheckCornerContainment(rect.Right, rect.Top) &&
               CheckCornerContainment(rect.Right, rect.Bottom) &&
               CheckCornerContainment(rect.Left, rect.Bottom);
    }

    public SKPoint GetRadii(SKRoundRectCorner corner) => _radii[(int)corner];

    public void Deflate(SKSize size) => Deflate(size.Width, size.Height);

    public void Deflate(float dx, float dy) => Inset(dx, dy);

    public void Inflate(SKSize size) => Inflate(size.Width, size.Height);

    public void Inflate(float dx, float dy) => Inset(-dx, -dy);

    public void Offset(SKPoint pos) => Offset(pos.X, pos.Y);

    public void Offset(float dx, float dy)
    {
        _rect.Offset(dx, dy);
    }

    public bool TryTransform(SKMatrix matrix, out SKRoundRect transformed)
    {
        if (matrix.IsIdentity)
        {
            transformed = new SKRoundRect(this);
            return true;
        }

        var isScaleTranslate = matrix.SkewX == 0f && matrix.SkewY == 0f;
        var isQuarterTurn = matrix.ScaleX == 0f && matrix.ScaleY == 0f &&
            matrix.SkewX != 0f && matrix.SkewY != 0f;
        if (matrix.Persp0 != 0f || matrix.Persp1 != 0f || matrix.Persp2 != 1f ||
            !isScaleTranslate && !isQuarterTurn)
        {
            transformed = null!;
            return false;
        }

        var newRect = matrix.MapRect(_rect);
        if (!RectIsFinite(newRect) || RectIsEmpty(newRect))
        {
            transformed = null!;
            return false;
        }

        transformed = new SKRoundRect
        {
            _rect = newRect,
            _type = _type,
        };
        if (_type == SKRoundRectType.Rect)
        {
            return true;
        }

        if (_type == SKRoundRectType.Oval)
        {
            Array.Fill(transformed._radii, new SKPoint(newRect.Width * 0.5f, newRect.Height * 0.5f));
            return true;
        }

        for (var source = 0; source < _radii.Length; source++)
        {
            var anchor = GetCornerAnchor(_rect, source);
            var center = GetCornerCenter(_rect, _radii[source], source);
            var mappedAnchor = matrix.MapPoint(anchor);
            var mappedCenter = matrix.MapPoint(center);
            var destination = GetCornerIndex(newRect, mappedAnchor);
            transformed._radii[destination] = new SKPoint(
                MathF.Abs(mappedCenter.X - mappedAnchor.X),
                MathF.Abs(mappedCenter.Y - mappedAnchor.Y));
        }

        transformed.ScaleRadii();
        if (!transformed.ValidateRectAndRadii())
        {
            transformed.Dispose();
            transformed = null!;
            return false;
        }

        return true;
    }

    public SKRoundRect Transform(SKMatrix matrix) =>
        TryTransform(matrix, out var transformed) ? transformed : null!;

    private void CopyFrom(SKRoundRect source)
    {
        _rect = source._rect;
        _type = source._type;
        Array.Copy(source._radii, _radii, _radii.Length);
    }

    private bool InitializeRect(SKRect rect)
    {
        if (!RectIsFinite(rect))
        {
            SetEmpty();
            return false;
        }

        _rect = rect.Standardized;
        if (RectIsEmpty(_rect))
        {
            Array.Clear(_radii);
            _type = SKRoundRectType.Empty;
            return false;
        }

        return true;
    }

    private void Inset(float dx, float dy)
    {
        var rect = new SKRect(
            _rect.Left + dx,
            _rect.Top + dy,
            _rect.Right - dx,
            _rect.Bottom - dy);
        var degenerate = false;
        if (rect.Right <= rect.Left)
        {
            degenerate = true;
            var center = (rect.Left + rect.Right) * 0.5f;
            rect.Left = center;
            rect.Right = center;
        }

        if (rect.Bottom <= rect.Top)
        {
            degenerate = true;
            var center = (rect.Top + rect.Bottom) * 0.5f;
            rect.Top = center;
            rect.Bottom = center;
        }

        if (degenerate)
        {
            _rect = rect;
            Array.Clear(_radii);
            _type = SKRoundRectType.Empty;
            return;
        }

        if (!RectIsFinite(rect))
        {
            SetEmpty();
            return;
        }

        Span<SKPoint> radii = stackalloc SKPoint[4];
        for (var index = 0; index < radii.Length; index++)
        {
            radii[index] = new SKPoint(
                _radii[index].X == 0f ? 0f : _radii[index].X - dx,
                _radii[index].Y == 0f ? 0f : _radii[index].Y - dy);
        }

        SetRectRadii(rect, radii);
    }

    private bool CheckCornerContainment(float x, float y)
    {
        SKRoundRectCorner corner;
        float canonicalX;
        float canonicalY;
        if (_type == SKRoundRectType.Oval)
        {
            corner = SKRoundRectCorner.UpperLeft;
            canonicalX = x - (_rect.Left + _rect.Right) * 0.5f;
            canonicalY = y - (_rect.Top + _rect.Bottom) * 0.5f;
        }
        else if (x < _rect.Left + _radii[0].X && y < _rect.Top + _radii[0].Y)
        {
            corner = SKRoundRectCorner.UpperLeft;
            canonicalX = x - (_rect.Left + _radii[0].X);
            canonicalY = y - (_rect.Top + _radii[0].Y);
        }
        else if (x < _rect.Left + _radii[3].X && y > _rect.Bottom - _radii[3].Y)
        {
            corner = SKRoundRectCorner.LowerLeft;
            canonicalX = x - (_rect.Left + _radii[3].X);
            canonicalY = y - (_rect.Bottom - _radii[3].Y);
        }
        else if (x > _rect.Right - _radii[1].X && y < _rect.Top + _radii[1].Y)
        {
            corner = SKRoundRectCorner.UpperRight;
            canonicalX = x - (_rect.Right - _radii[1].X);
            canonicalY = y - (_rect.Top + _radii[1].Y);
        }
        else if (x > _rect.Right - _radii[2].X && y > _rect.Bottom - _radii[2].Y)
        {
            corner = SKRoundRectCorner.LowerRight;
            canonicalX = x - (_rect.Right - _radii[2].X);
            canonicalY = y - (_rect.Bottom - _radii[2].Y);
        }
        else
        {
            return true;
        }

        var radius = _radii[(int)corner];
        var distance = canonicalX * canonicalX * radius.Y * radius.Y +
            canonicalY * canonicalY * radius.X * radius.X;
        var product = radius.X * radius.Y;
        return distance <= product * product;
    }

    private void ScaleRadii()
    {
        var width = (double)_rect.Right - _rect.Left;
        var height = (double)_rect.Bottom - _rect.Top;
        var scale = 1d;
        scale = ComputeMinimumScale(_radii[0].X, _radii[1].X, width, scale);
        scale = ComputeMinimumScale(_radii[1].Y, _radii[2].Y, height, scale);
        scale = ComputeMinimumScale(_radii[2].X, _radii[3].X, width, scale);
        scale = ComputeMinimumScale(_radii[3].Y, _radii[0].Y, height, scale);
        FlushToZero(0, true, 1, true);
        FlushToZero(1, false, 2, false);
        FlushToZero(2, true, 3, true);
        FlushToZero(3, false, 0, false);
        if (scale < 1d)
        {
            AdjustRadii(width, scale, 0, true, 1, true);
            AdjustRadii(height, scale, 1, false, 2, false);
            AdjustRadii(width, scale, 2, true, 3, true);
            AdjustRadii(height, scale, 3, false, 0, false);
        }

        ClampSquareCorners();
        ComputeType();
    }

    private void ClampSquareCorners()
    {
        for (var index = 0; index < _radii.Length; index++)
        {
            if (_radii[index].X <= 0f || _radii[index].Y <= 0f)
            {
                _radii[index] = default;
            }
        }
    }

    private void ComputeType()
    {
        if (RectIsEmpty(_rect))
        {
            Array.Clear(_radii);
            _type = SKRoundRectType.Empty;
            return;
        }

        var allRadiiEqual = true;
        var allCornersSquare = _radii[0].X == 0f || _radii[0].Y == 0f;
        for (var index = 1; index < _radii.Length; index++)
        {
            if (_radii[index].X != 0f && _radii[index].Y != 0f)
            {
                allCornersSquare = false;
            }

            if (_radii[index] != _radii[index - 1])
            {
                allRadiiEqual = false;
            }
        }

        if (allCornersSquare)
        {
            Array.Clear(_radii);
            _type = SKRoundRectType.Rect;
        }
        else if (allRadiiEqual)
        {
            _type = _radii[0].X >= _rect.Width * 0.5f && _radii[0].Y >= _rect.Height * 0.5f
                ? SKRoundRectType.Oval
                : SKRoundRectType.Simple;
        }
        else
        {
            _type = RadiiAreNinePatch() ? SKRoundRectType.NinePatch : SKRoundRectType.Complex;
        }
    }

    private bool Validate()
    {
        if (!ValidateRectAndRadii())
        {
            return false;
        }

        var expectedType = _type;
        ComputeType();
        var valid = _type == expectedType;
        _type = expectedType;
        return valid;
    }

    private bool ValidateRectAndRadii()
    {
        if (!RectIsFinite(_rect) || _rect.Left > _rect.Right || _rect.Top > _rect.Bottom)
        {
            return false;
        }

        foreach (var radius in _radii)
        {
            if (!float.IsFinite(radius.X) || !float.IsFinite(radius.Y) ||
                radius.X < 0f || radius.Y < 0f ||
                radius.X > _rect.Width || radius.Y > _rect.Height)
            {
                return false;
            }
        }

        return true;
    }

    private bool RadiiAreNinePatch() =>
        _radii[0].X == _radii[3].X &&
        _radii[0].Y == _radii[1].Y &&
        _radii[1].X == _radii[2].X &&
        _radii[3].Y == _radii[2].Y;

    private static bool RectIsFinite(SKRect rect) =>
        float.IsFinite(rect.Left) &&
        float.IsFinite(rect.Top) &&
        float.IsFinite(rect.Right) &&
        float.IsFinite(rect.Bottom);

    private static bool RectIsEmpty(SKRect rect) => rect.Right <= rect.Left || rect.Bottom <= rect.Top;

    private static bool NearlyEqual(float left, float right, float tolerance) =>
        MathF.Abs(left - right) <= tolerance;

    private static double ComputeMinimumScale(double first, double second, double limit, double current) =>
        first + second > limit ? Math.Min(current, limit / (first + second)) : current;

    private static SKPoint GetCornerAnchor(SKRect rect, int corner) => corner switch
    {
        0 => new SKPoint(rect.Left, rect.Top),
        1 => new SKPoint(rect.Right, rect.Top),
        2 => new SKPoint(rect.Right, rect.Bottom),
        3 => new SKPoint(rect.Left, rect.Bottom),
        _ => throw new ArgumentOutOfRangeException(nameof(corner)),
    };

    private static SKPoint GetCornerCenter(SKRect rect, SKPoint radius, int corner) => corner switch
    {
        0 => new SKPoint(rect.Left + radius.X, rect.Top + radius.Y),
        1 => new SKPoint(rect.Right - radius.X, rect.Top + radius.Y),
        2 => new SKPoint(rect.Right - radius.X, rect.Bottom - radius.Y),
        3 => new SKPoint(rect.Left + radius.X, rect.Bottom - radius.Y),
        _ => throw new ArgumentOutOfRangeException(nameof(corner)),
    };

    private static int GetCornerIndex(SKRect rect, SKPoint anchor)
    {
        var isLeft = anchor.X <= (rect.Left + rect.Right) * 0.5f;
        var isTop = anchor.Y <= (rect.Top + rect.Bottom) * 0.5f;
        return isTop ? isLeft ? 0 : 1 : isLeft ? 3 : 2;
    }

    private void FlushToZero(int firstIndex, bool firstXAxis, int secondIndex, bool secondXAxis)
    {
        var first = firstXAxis ? _radii[firstIndex].X : _radii[firstIndex].Y;
        var second = secondXAxis ? _radii[secondIndex].X : _radii[secondIndex].Y;
        if (first + second == first)
        {
            SetRadiusComponent(secondIndex, secondXAxis, 0f);
        }
        else if (first + second == second)
        {
            SetRadiusComponent(firstIndex, firstXAxis, 0f);
        }
    }

    private void AdjustRadii(
        double limit,
        double scale,
        int firstIndex,
        bool firstXAxis,
        int secondIndex,
        bool secondXAxis)
    {
        var first = (float)((double)(firstXAxis ? _radii[firstIndex].X : _radii[firstIndex].Y) * scale);
        var second = (float)((double)(secondXAxis ? _radii[secondIndex].X : _radii[secondIndex].Y) * scale);
        if (first + second > limit)
        {
            var firstIsMinimum = first <= second;
            var minimum = firstIsMinimum ? first : second;
            var maximum = (float)(limit - minimum);
            while (maximum + minimum > limit)
            {
                maximum = MathF.BitDecrement(maximum);
            }

            if (firstIsMinimum)
            {
                second = maximum;
            }
            else
            {
                first = maximum;
            }
        }

        SetRadiusComponent(firstIndex, firstXAxis, first);
        SetRadiusComponent(secondIndex, secondXAxis, second);
    }

    private void SetRadiusComponent(int index, bool xAxis, float value)
    {
        var radius = _radii[index];
        _radii[index] = xAxis
            ? new SKPoint(value, radius.Y)
            : new SKPoint(radius.X, value);
    }

}
