using System;
using System.Numerics;
using ProGPU.Layout;
using ProGPU.Vector;
using ProGPU.Scene;

namespace ProGPU.WinUI;

public class Button : Control
{
    private FrameworkElement? _content;

    public FrameworkElement? Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                if (_content != null) RemoveChild(_content);
                _content = value;
                if (_content != null) AddChild(_content);
                Invalidate();
            }
        }
    }

    public event EventHandler? Click;

    public Button()
    {
        CornerRadius = 6f;
        Padding = new Thickness(12, 6, 12, 6);
    }

    public override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        if (IsEnabled && IsPointerPressed && IsPointerOver)
        {
            Click?.Invoke(this, EventArgs.Empty);
        }
        base.OnPointerReleased(e);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        float borderH = BorderThickness.Horizontal;
        float borderV = BorderThickness.Vertical;
        float paddingH = Padding.Horizontal;
        float paddingV = Padding.Vertical;

        Vector2 inset = new Vector2(borderH + paddingH, borderV + paddingV);
        Vector2 contentAvail = new Vector2(
            Math.Max(0f, availableSize.X - inset.X),
            Math.Max(0f, availableSize.Y - inset.Y)
        );

        Vector2 contentDesired = Vector2.Zero;
        if (Content != null)
        {
            Content.Measure(contentAvail);
            contentDesired = Content.DesiredSize;
        }

        float minW = 64f;
        float minH = 28f;
        return new Vector2(
            Math.Max(minW, contentDesired.X + inset.X),
            Math.Max(minH, contentDesired.Y + inset.Y)
        );
    }

    protected override void ArrangeOverride(Rect arrangeRect)
    {
        if (Content != null)
        {
            float leftInset = BorderThickness.Left + Padding.Left;
            float topInset = BorderThickness.Top + Padding.Top;
            float rightInset = BorderThickness.Right + Padding.Right;
            float bottomInset = BorderThickness.Bottom + Padding.Bottom;

            float childW = Math.Min(arrangeRect.Width - (leftInset + rightInset), Content.DesiredSize.X);
            float childH = Math.Min(arrangeRect.Height - (topInset + bottomInset), Content.DesiredSize.Y);

            float childX = arrangeRect.X + leftInset + (arrangeRect.Width - (leftInset + rightInset) - childW) / 2f;
            float childY = arrangeRect.Y + topInset + (arrangeRect.Height - (topInset + bottomInset) - childH) / 2f;

            Content.Arrange(new Rect(childX, childY, childW, childH));
        }
    }

    public override void OnRender(DrawingContext context)
    {
        Brush? bg = Background;
        if (!IsEnabled)
        {
            bg = new SolidColorBrush(0x2A2A3540);
        }
        else if (IsPointerPressed)
        {
            bg = new SolidColorBrush(0xFFFFFF0D); // darker translucent
        }
        else if (IsPointerOver)
        {
            bg = new SolidColorBrush(0xFFFFFF25); // lighter translucent hover
        }
        else
        {
            bg = Background ?? new SolidColorBrush(0xFFFFFF15); // normal glassmorphic background
        }

        var pen = BorderBrush != null && BorderThickness.Left > 0 
            ? new Pen(BorderBrush, BorderThickness.Left) 
            : new Pen(new SolidColorBrush(IsEnabled ? 0xFFFFFF20 : 0xFFFFFF0F), 1f);

        if (CornerRadius <= 0f)
        {
            context.DrawRectangle(bg, pen, new Rect(Vector2.Zero, Size));
        }
        else
        {
            var roundedPath = CreateRoundedRectPath(new Rect(Vector2.Zero, Size), CornerRadius);
            context.DrawPath(bg, pen, roundedPath);
        }

        base.OnRender(context);
    }

    private static PathGeometry CreateRoundedRectPath(Rect rect, float r)
    {
        var geo = new PathGeometry();
        var fig = new PathFigure(new Vector2(rect.X + r, rect.Y), isClosed: true);
        fig.Segments.Add(new LineSegment(new Vector2(rect.X + rect.Width - r, rect.Y)));
        fig.Segments.Add(new QuadraticBezierSegment(new Vector2(rect.X + rect.Width, rect.Y), new Vector2(rect.X + rect.Width, rect.Y + r)));
        fig.Segments.Add(new LineSegment(new Vector2(rect.X + rect.Width, rect.Y + rect.Height - r)));
        fig.Segments.Add(new QuadraticBezierSegment(new Vector2(rect.X + rect.Width, rect.Y + rect.Height), new Vector2(rect.X + rect.Width - r, rect.Y + rect.Height)));
        fig.Segments.Add(new LineSegment(new Vector2(rect.X + r, rect.Y + rect.Height)));
        fig.Segments.Add(new QuadraticBezierSegment(new Vector2(rect.X, rect.Y + rect.Height), new Vector2(rect.X, rect.Y + rect.Height - r)));
        fig.Segments.Add(new LineSegment(new Vector2(rect.X, rect.Y + r)));
        fig.Segments.Add(new QuadraticBezierSegment(new Vector2(rect.X, rect.Y), new Vector2(rect.X + r, rect.Y)));
        geo.Figures.Add(fig);
        return geo;
    }
}
