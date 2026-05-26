using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Numerics;
using ProGPU.Layout;
using ProGPU.Vector;
using ProGPU.Scene;

namespace Microsoft.UI.Xaml.Controls;

[ContentProperty(Name = "Child")]
public class Border : FrameworkElement
{
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            "Background",
            typeof(Brush),
            typeof(Border),
            new PropertyMetadata(null, (d, e) => ((Border)d).Invalidate()));

    public Brush? Background
    {
        get => GetValue(BackgroundProperty) as Brush;
        set => SetValue(BackgroundProperty, value);
    }

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            "BorderBrush",
            typeof(Brush),
            typeof(Border),
            new PropertyMetadata(null, (d, e) => ((Border)d).Invalidate()));

    public Brush? BorderBrush
    {
        get => GetValue(BorderBrushProperty) as Brush;
        set => SetValue(BorderBrushProperty, value);
    }

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            "BorderThickness",
            typeof(Thickness),
            typeof(Border),
            new PropertyMetadata(default(Thickness), (d, e) => {
                var b = (Border)d;
                b.Invalidate();
                b.InvalidateMeasure();
            }));

    public Thickness BorderThickness
    {
        get => (Thickness)(GetValue(BorderThicknessProperty) ?? default(Thickness));
        set => SetValue(BorderThicknessProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            "CornerRadius",
            typeof(float),
            typeof(Border),
            new PropertyMetadata(0f, (d, e) => ((Border)d).Invalidate()));

    public float CornerRadius
    {
        get => (float)(GetValue(CornerRadiusProperty) ?? 0f);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty ChildProperty =
        DependencyProperty.Register(
            "Child",
            typeof(FrameworkElement),
            typeof(Border),
            new PropertyMetadata(null, (d, e) => ((Border)d).OnChildChanged(e.OldValue as FrameworkElement, e.NewValue as FrameworkElement)));

    public FrameworkElement? Child
    {
        get => GetValue(ChildProperty) as FrameworkElement;
        set => SetValue(ChildProperty, value);
    }

    private void OnChildChanged(FrameworkElement? oldValue, FrameworkElement? newValue)
    {
        if (oldValue != null) RemoveChild(oldValue);
        if (newValue != null) AddChild(newValue);
        Invalidate();
        InvalidateMeasure();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        float borderH = BorderThickness.Horizontal;
        float borderV = BorderThickness.Vertical;
        float paddingH = Padding.Horizontal;
        float paddingV = Padding.Vertical;

        Vector2 inset = new Vector2(borderH + paddingH, borderV + paddingV);
        Vector2 childAvailable = new Vector2(
            Math.Max(0f, availableSize.X - inset.X),
            Math.Max(0f, availableSize.Y - inset.Y)
        );

        Vector2 childDesired = Vector2.Zero;
        if (Child != null)
        {
            Child.Measure(childAvailable);
            childDesired = Child.DesiredSize;
        }

        return childDesired + new Vector2(borderH, borderV);
    }

    protected override void ArrangeOverride(Rect arrangeRect)
    {
        if (Child != null)
        {
            float leftInset = BorderThickness.Left;
            float topInset = BorderThickness.Top;
            float rightInset = BorderThickness.Right;
            float bottomInset = BorderThickness.Bottom;

            Rect childRect = new Rect(
                arrangeRect.X + leftInset,
                arrangeRect.Y + topInset,
                Math.Max(0f, arrangeRect.Width - (leftInset + rightInset)),
                Math.Max(0f, arrangeRect.Height - (topInset + bottomInset))
            );
            Child.Arrange(childRect);
        }
    }

    public override void OnRender(DrawingContext context)
    {
        if (Background != null || (BorderBrush != null && BorderThickness.Left > 0))
        {
            var pen = BorderBrush != null && BorderThickness.Left > 0 ? new Pen(BorderBrush, BorderThickness.Left) : null;
            context.DrawRoundedRectangle(Background, pen, new Rect(Vector2.Zero, Size), CornerRadius);
        }
        base.OnRender(context);
    }
}
