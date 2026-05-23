using System;
using System.Numerics;
using ProGPU.Layout;
using ProGPU.Scene;
using ProGPU.Vector;

namespace ProGPU.WinUI;

public class ProgressRing : Control
{
    private bool _isActive = true;
    private float _rotationOffset;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                Invalidate();
            }
        }
    }

    public ProgressRing()
    {
        Width = 32f;
        Height = 32f;
        Background = new SolidColorBrush(0x00000000); // Fully transparent container
        BorderBrush = new SolidColorBrush(0x0078D4FF); // Segoe Accent Blue dots
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        return new Vector2(Width, Height);
    }

    public override void OnRender(DrawingContext context)
    {
        if (IsActive)
        {
            float cx = Size.X / 2f;
            float cy = Size.Y / 2f;
            float radius = (Size.X - 6f) / 2f; // Keep dot bounds inside control
            float dotRadius = 3f;

            // Draw 8 circular dots in a loop with a tail fading opacity sweep
            for (int i = 0; i < 8; i++)
            {
                // Angle with dynamic rotation sweep offset
                float angle = (float)(i * (2.0 * Math.PI / 8.0) + _rotationOffset);
                float x = cx + radius * (float)Math.Cos(angle);
                float y = cy + radius * (float)Math.Sin(angle);

                // Sweep opacity: creates a fading trail of loaded dots
                float opacityFraction = (i / 8f);
                uint alpha = (uint)(255 * opacityFraction);
                
                // Segoe Accent Blue with dynamic alpha opacity
                var dotColor = new SolidColorBrush(0x0078D400 | alpha);

                var dotPath = CreateRoundedRect(x - dotRadius, y - dotRadius, dotRadius * 2f, dotRadius * 2f, dotRadius);
                context.DrawPath(dotColor, null, dotPath);
            }

            // Animate spin speed smoothly at 60 FPS
            _rotationOffset = (float)((_rotationOffset + 0.08f) % (2.0 * Math.PI));

            // Self-invalidate to trigger continuous render presents on the WebGPU surface
            Invalidate();
        }

        base.OnRender(context);
    }

    private static PathGeometry CreateRoundedRect(float x, float y, float w, float h, float r)
    {
        return PathGeometry.Parse(System.FormattableString.Invariant($"M {x+r} {y} H {x+w-r} Q {x+w} {y} {x+w} {y+r} V {y+h-r} Q {x+w} {y+h} {x+w-r} {y+h} H {x+r} Q {x} {y+h} {x} {y+h-r} V {y+r} Q {x} {y} {x+r} {y} Z"));
    }
}
