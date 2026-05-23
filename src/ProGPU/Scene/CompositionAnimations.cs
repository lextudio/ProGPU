using System;
using System.Collections.Generic;
using System.Numerics;

namespace ProGPU.Scene;

/// <summary>
/// Defines how keyframe animations behave when they reach the end of their duration.
/// </summary>
public enum AnimationIterationBehavior
{
    Single,
    Forever
}

/// <summary>
/// Abstract base class for all composition easing functions.
/// </summary>
public abstract class CompositionEasingFunction
{
    /// <summary>
    /// Eases the normalized progress (0.0 to 1.0) according to the easing algorithm.
    /// </summary>
    public abstract float Ease(float progress);
}

/// <summary>
/// Linearly interpolates progress.
/// </summary>
public class LinearEasingFunction : CompositionEasingFunction
{
    public override float Ease(float progress) => progress;
}

/// <summary>
/// Computes cubic Bezier splines using control points with standard de Casteljau parametric math.
/// </summary>
public class CubicBezierEasingFunction : CompositionEasingFunction
{
    public Vector2 ControlPoint1 { get; }
    public Vector2 ControlPoint2 { get; }

    public CubicBezierEasingFunction(Vector2 controlPoint1, Vector2 controlPoint2)
    {
        ControlPoint1 = controlPoint1;
        ControlPoint2 = controlPoint2;
    }

    public override float Ease(float progress)
    {
        if (progress <= 0f) return 0f;
        if (progress >= 1f) return 1f;

        // Solve for the parametric parameter t corresponding to the given progress (x-coordinate)
        float t = SolveT(progress);
        return GetY(t);
    }

    /// <summary>
    /// Solves for parametric parameter t given target progress x using Newton-Raphson 
    /// with a bisection (binary search) fallback to ensure high numerical stability.
    /// </summary>
    private float SolveT(float x)
    {
        // 1. Try Newton-Raphson approximation
        float t = x;
        for (int i = 0; i < 8; i++)
        {
            float currentX = GetX(t) - x;
            float slope = GetSlopeX(t);
            if (MathF.Abs(slope) < 1e-6f)
                break;
            t -= currentX / slope;
        }

        // 2. Fall back to bisection search if Newton-Raphson diverged or went out of range
        if (t < 0f || t > 1f)
        {
            float low = 0f;
            float high = 1f;
            t = x;

            for (int i = 0; i < 16; i++)
            {
                float currentX = GetX(t);
                if (MathF.Abs(currentX - x) < 1e-6f)
                    break;
                if (x > currentX)
                    low = t;
                else
                    high = t;
                t = (low + high) * 0.5f;
            }
        }

        return Math.Clamp(t, 0f, 1f);
    }

    /// <summary>
    /// Evaluates the x-coordinate of the parametric Bezier curve using de Casteljau's math.
    /// B(t) = (1-t)^3*P0 + 3(1-t)^2*t*P1 + 3(1-t)*t^2*P2 + t^3*P3
    /// where P0 = (0,0) and P3 = (1,1).
    /// </summary>
    private float GetX(float t)
    {
        float tSq = t * t;
        float oneMinusT = 1f - t;
        float oneMinusTSq = oneMinusT * oneMinusT;

        // Parametric evaluation:
        return 3f * oneMinusTSq * t * ControlPoint1.X + 3f * oneMinusT * tSq * ControlPoint2.X + tSq * t;
    }

    /// <summary>
    /// Computes the derivative dx/dt for Newton-Raphson root finding.
    /// </summary>
    private float GetSlopeX(float t)
    {
        float oneMinusT = 1f - t;
        return 3f * oneMinusT * oneMinusT * ControlPoint1.X +
               6f * oneMinusT * t * (ControlPoint2.X - ControlPoint1.X) +
               3f * t * t * (1f - ControlPoint2.X);
    }

    /// <summary>
    /// Evaluates the y-coordinate of the parametric Bezier curve using de Casteljau's math.
    /// </summary>
    private float GetY(float t)
    {
        float tSq = t * t;
        float oneMinusT = 1f - t;
        float oneMinusTSq = oneMinusT * oneMinusT;

        return 3f * oneMinusTSq * t * ControlPoint1.Y + 3f * oneMinusT * tSq * ControlPoint2.Y + tSq * t;
    }
}

/// <summary>
/// Steps along discrete intervals in the animation progress.
/// </summary>
public class StepEasingFunction : CompositionEasingFunction
{
    public int StepCount { get; }

    public StepEasingFunction(int stepCount)
    {
        if (stepCount < 1) stepCount = 1;
        StepCount = stepCount;
    }

    public override float Ease(float progress)
    {
        if (progress <= 0f) return 0f;
        if (progress >= 1f) return 1f;

        float stepWidth = 1f / StepCount;
        float currentStep = MathF.Floor(progress / stepWidth);
        return currentStep / StepCount;
    }
}

/// <summary>
/// Abstract base class for all composition animations.
/// </summary>
public abstract class CompositionAnimation
{
    public abstract object CurrentValue { get; }
    public abstract bool IsCompleted { get; }

    /// <summary>
    /// Advances the animation time by the specified duration in seconds.
    /// </summary>
    public abstract void Tick(float elapsedSeconds);
}

/// <summary>
/// Base class for all keyframe animations, supporting delay time, duration, and loop behaviors.
/// </summary>
public abstract class KeyFrameAnimation : CompositionAnimation
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(1);
    public AnimationIterationBehavior IterationBehavior { get; set; } = AnimationIterationBehavior.Single;
    public TimeSpan DelayTime { get; set; } = TimeSpan.Zero;

    protected float _accumulatedTime = 0f;
    protected bool _isCompleted = false;

    public override bool IsCompleted => _isCompleted;
}

/// <summary>
/// A representation of a single KeyFrame with progress, value, and easing behavior.
/// </summary>
internal class KeyFrame<T>
{
    public float Progress { get; }
    public T Value { get; }
    public CompositionEasingFunction? Easing { get; }

    public KeyFrame(float progress, T value, CompositionEasingFunction? easing)
    {
        Progress = progress;
        Value = value;
        Easing = easing;
    }
}

/// <summary>
/// A keyframe animation for scalar (float) values.
/// </summary>
public class ScalarKeyFrameAnimation : KeyFrameAnimation
{
    private readonly List<KeyFrame<float>> _keyframes = new();
    private float _currentValue;

    public override object CurrentValue => _currentValue;

    public void InsertKeyFrame(float progress, float value, CompositionEasingFunction? easing = null)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        _keyframes.Add(new KeyFrame<float>(progress, value, easing));
        _keyframes.Sort((a, b) => a.Progress.CompareTo(b.Progress));
    }

    public override void Tick(float elapsedSeconds)
    {
        if (_keyframes.Count == 0)
        {
            _currentValue = 0f;
            return;
        }

        _accumulatedTime += elapsedSeconds;

        float delaySec = (float)DelayTime.TotalSeconds;
        float durationSec = (float)Duration.TotalSeconds;

        float progress = 0f;

        if (_accumulatedTime < delaySec)
        {
            progress = 0f;
        }
        else
        {
            float progressTime = _accumulatedTime - delaySec;
            if (durationSec <= 0f)
            {
                progress = 1f;
                if (IterationBehavior == AnimationIterationBehavior.Single)
                {
                    _isCompleted = true;
                }
            }
            else if (progressTime >= durationSec)
            {
                if (IterationBehavior == AnimationIterationBehavior.Forever)
                {
                    progressTime %= durationSec;
                    progress = progressTime / durationSec;
                }
                else
                {
                    progress = 1f;
                    _isCompleted = true;
                }
            }
            else
            {
                progress = progressTime / durationSec;
            }
        }

        _currentValue = Interpolate(progress);
    }

    private float Interpolate(float progress)
    {
        if (_keyframes.Count == 0) return 0f;
        if (_keyframes.Count == 1) return _keyframes[0].Value;

        if (progress <= _keyframes[0].Progress)
            return _keyframes[0].Value;

        if (progress >= _keyframes[^1].Progress)
            return _keyframes[^1].Value;

        int index = 0;
        for (int i = 0; i < _keyframes.Count - 1; i++)
        {
            if (progress >= _keyframes[i].Progress && progress <= _keyframes[i + 1].Progress)
            {
                index = i;
                break;
            }
        }

        var k0 = _keyframes[index];
        var k1 = _keyframes[index + 1];

        float range = k1.Progress - k0.Progress;
        float t = range <= 0f ? 1f : (progress - k0.Progress) / range;

        if (k1.Easing != null)
        {
            t = k1.Easing.Ease(t);
        }

        return k0.Value + (k1.Value - k0.Value) * t;
    }
}

/// <summary>
/// A keyframe animation for Vector2 values.
/// </summary>
public class Vector2KeyFrameAnimation : KeyFrameAnimation
{
    private readonly List<KeyFrame<Vector2>> _keyframes = new();
    private Vector2 _currentValue;

    public override object CurrentValue => _currentValue;

    public void InsertKeyFrame(float progress, Vector2 value, CompositionEasingFunction? easing = null)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        _keyframes.Add(new KeyFrame<Vector2>(progress, value, easing));
        _keyframes.Sort((a, b) => a.Progress.CompareTo(b.Progress));
    }

    public override void Tick(float elapsedSeconds)
    {
        if (_keyframes.Count == 0)
        {
            _currentValue = Vector2.Zero;
            return;
        }

        _accumulatedTime += elapsedSeconds;

        float delaySec = (float)DelayTime.TotalSeconds;
        float durationSec = (float)Duration.TotalSeconds;

        float progress = 0f;

        if (_accumulatedTime < delaySec)
        {
            progress = 0f;
        }
        else
        {
            float progressTime = _accumulatedTime - delaySec;
            if (durationSec <= 0f)
            {
                progress = 1f;
                if (IterationBehavior == AnimationIterationBehavior.Single)
                {
                    _isCompleted = true;
                }
            }
            else if (progressTime >= durationSec)
            {
                if (IterationBehavior == AnimationIterationBehavior.Forever)
                {
                    progressTime %= durationSec;
                    progress = progressTime / durationSec;
                }
                else
                {
                    progress = 1f;
                    _isCompleted = true;
                }
            }
            else
            {
                progress = progressTime / durationSec;
            }
        }

        _currentValue = Interpolate(progress);
    }

    private Vector2 Interpolate(float progress)
    {
        if (_keyframes.Count == 0) return Vector2.Zero;
        if (_keyframes.Count == 1) return _keyframes[0].Value;

        if (progress <= _keyframes[0].Progress)
            return _keyframes[0].Value;

        if (progress >= _keyframes[^1].Progress)
            return _keyframes[^1].Value;

        int index = 0;
        for (int i = 0; i < _keyframes.Count - 1; i++)
        {
            if (progress >= _keyframes[i].Progress && progress <= _keyframes[i + 1].Progress)
            {
                index = i;
                break;
            }
        }

        var k0 = _keyframes[index];
        var k1 = _keyframes[index + 1];

        float range = k1.Progress - k0.Progress;
        float t = range <= 0f ? 1f : (progress - k0.Progress) / range;

        if (k1.Easing != null)
        {
            t = k1.Easing.Ease(t);
        }

        return Vector2.Lerp(k0.Value, k1.Value, t);
    }
}

/// <summary>
/// A keyframe animation for Vector3 values.
/// </summary>
public class Vector3KeyFrameAnimation : KeyFrameAnimation
{
    private readonly List<KeyFrame<Vector3>> _keyframes = new();
    private Vector3 _currentValue;

    public override object CurrentValue => _currentValue;

    public void InsertKeyFrame(float progress, Vector3 value, CompositionEasingFunction? easing = null)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        _keyframes.Add(new KeyFrame<Vector3>(progress, value, easing));
        _keyframes.Sort((a, b) => a.Progress.CompareTo(b.Progress));
    }

    public override void Tick(float elapsedSeconds)
    {
        if (_keyframes.Count == 0)
        {
            _currentValue = Vector3.Zero;
            return;
        }

        _accumulatedTime += elapsedSeconds;

        float delaySec = (float)DelayTime.TotalSeconds;
        float durationSec = (float)Duration.TotalSeconds;

        float progress = 0f;

        if (_accumulatedTime < delaySec)
        {
            progress = 0f;
        }
        else
        {
            float progressTime = _accumulatedTime - delaySec;
            if (durationSec <= 0f)
            {
                progress = 1f;
                if (IterationBehavior == AnimationIterationBehavior.Single)
                {
                    _isCompleted = true;
                }
            }
            else if (progressTime >= durationSec)
            {
                if (IterationBehavior == AnimationIterationBehavior.Forever)
                {
                    progressTime %= durationSec;
                    progress = progressTime / durationSec;
                }
                else
                {
                    progress = 1f;
                    _isCompleted = true;
                }
            }
            else
            {
                progress = progressTime / durationSec;
            }
        }

        _currentValue = Interpolate(progress);
    }

    private Vector3 Interpolate(float progress)
    {
        if (_keyframes.Count == 0) return Vector3.Zero;
        if (_keyframes.Count == 1) return _keyframes[0].Value;

        if (progress <= _keyframes[0].Progress)
            return _keyframes[0].Value;

        if (progress >= _keyframes[^1].Progress)
            return _keyframes[^1].Value;

        int index = 0;
        for (int i = 0; i < _keyframes.Count - 1; i++)
        {
            if (progress >= _keyframes[i].Progress && progress <= _keyframes[i + 1].Progress)
            {
                index = i;
                break;
            }
        }

        var k0 = _keyframes[index];
        var k1 = _keyframes[index + 1];

        float range = k1.Progress - k0.Progress;
        float t = range <= 0f ? 1f : (progress - k0.Progress) / range;

        if (k1.Easing != null)
        {
            t = k1.Easing.Ease(t);
        }

        return Vector3.Lerp(k0.Value, k1.Value, t);
    }
}

/// <summary>
/// Evaluates physical spring harmonic oscillator equations dynamically, wobbling smoothly towards FinalValue.
/// Supports underdamped, critically damped, and overdamped motion states.
/// </summary>
public class SpringScalarNaturalMotionAnimation : CompositionAnimation
{
    private float _currentValue;
    private bool _isCompleted;
    private float _accumulatedTime;

    private float _dampingRatio = 0.5f;
    private float _period = 0.5f;
    private float _initialVelocity = 0f;
    private float _initialValue = 0f;
    private float _finalValue = 1f;

    public float DampingRatio
    {
        get => _dampingRatio;
        set
        {
            if (_dampingRatio != value)
            {
                _dampingRatio = value;
                Reset();
            }
        }
    }

    public float Period
    {
        get => _period;
        set
        {
            if (_period != value)
            {
                _period = value;
                Reset();
            }
        }
    }

    public float InitialVelocity
    {
        get => _initialVelocity;
        set
        {
            if (_initialVelocity != value)
            {
                _initialVelocity = value;
                Reset();
            }
        }
    }

    public float InitialValue
    {
        get => _initialValue;
        set
        {
            if (_initialValue != value)
            {
                _initialValue = value;
                _currentValue = value;
                Reset();
            }
        }
    }

    public float FinalValue
    {
        get => _finalValue;
        set
        {
            if (_finalValue != value)
            {
                _finalValue = value;
                Reset();
            }
        }
    }

    public override object CurrentValue => _currentValue;
    public override bool IsCompleted => _isCompleted;

    public SpringScalarNaturalMotionAnimation()
    {
        _currentValue = _initialValue;
    }

    private void Reset()
    {
        _accumulatedTime = 0f;
        _isCompleted = false;
    }

    public override void Tick(float elapsedSeconds)
    {
        if (_isCompleted)
        {
            _currentValue = FinalValue;
            return;
        }

        _accumulatedTime += elapsedSeconds;
        float t = _accumulatedTime;

        float x0 = InitialValue;
        float xf = FinalValue;
        float v0 = InitialVelocity;

        float dampingRatio = DampingRatio;
        float period = Period;

        if (period <= 0f)
        {
            _currentValue = xf;
            _isCompleted = true;
            return;
        }

        // Undamped natural angular frequency: omega0 = 2*pi / T
        float omega0 = (2f * MathF.PI) / period;

        float x;
        float v;

        if (dampingRatio < 1f)
        {
            // Underdamped motion
            float omegaD = omega0 * MathF.Sqrt(1f - dampingRatio * dampingRatio);
            float a = x0 - xf;
            float b = (v0 + dampingRatio * omega0 * a) / omegaD;

            float exp = MathF.Exp(-dampingRatio * omega0 * t);
            float cos = MathF.Cos(omegaD * t);
            float sin = MathF.Sin(omegaD * t);

            x = xf + exp * (a * cos + b * sin);
            v = exp * ((b * omegaD - dampingRatio * omega0 * a) * cos - (a * omegaD + dampingRatio * omega0 * b) * sin);
        }
        else if (MathF.Abs(dampingRatio - 1f) < 1e-5f)
        {
            // Critically damped motion
            float a = x0 - xf;
            float b = v0 + omega0 * a;

            float exp = MathF.Exp(-omega0 * t);

            x = xf + exp * (a + b * t);
            v = exp * (b - omega0 * a - omega0 * b * t);
        }
        else
        {
            // Overdamped motion
            float omegaH = omega0 * MathF.Sqrt(dampingRatio * dampingRatio - 1f);
            float gamma1 = -dampingRatio * omega0 + omegaH;
            float gamma2 = -dampingRatio * omega0 - omegaH;

            float a = x0 - xf;
            // C1 + C2 = a
            // C1 * gamma1 + C2 * gamma2 = v0
            float c1 = (v0 - a * gamma2) / (gamma1 - gamma2);
            float c2 = a - c1;

            float exp1 = MathF.Exp(gamma1 * t);
            float exp2 = MathF.Exp(gamma2 * t);

            x = xf + c1 * exp1 + c2 * exp2;
            v = c1 * gamma1 * exp1 + c2 * gamma2 * exp2;
        }

        _currentValue = x;

        // Settlement condition: displacement and velocity are both negligible
        if (MathF.Abs(x - xf) < 1e-3f && MathF.Abs(v) < 1e-3f)
        {
            _currentValue = xf;
            _isCompleted = true;
        }
    }
}

/// <summary>
/// Evaluates a delegate-based mathematical expression dynamically in Tick.
/// </summary>
public class ExpressionAnimation : CompositionAnimation
{
    private Func<object>? _expression;
    private object _currentValue = 0f;

    public override object CurrentValue => _currentValue;
    public override bool IsCompleted => false;

    public void SetExpression(Func<object> expression)
    {
        _expression = expression;
    }

    public override void Tick(float elapsedSeconds)
    {
        if (_expression != null)
        {
            _currentValue = _expression();
        }
    }
}
