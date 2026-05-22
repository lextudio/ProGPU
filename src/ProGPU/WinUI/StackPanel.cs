using System;
using System.Numerics;
using ProGPU.Layout;
using ProGPU.Scene;

namespace ProGPU.WinUI;

public class StackPanel : Panel
{
    private Orientation _orientation = Orientation.Vertical;

    public Orientation Orientation
    {
        get => _orientation;
        set { if (_orientation != value) { _orientation = value; Invalidate(); } }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        float totalWidth = 0f;
        float totalHeight = 0f;

        foreach (var child in Children)
        {
            if (child is LayoutNode node)
            {
                if (Orientation == Orientation.Vertical)
                {
                    node.Measure(new Vector2(availableSize.X, float.PositiveInfinity));
                    var desired = node.DesiredSize;
                    totalWidth = Math.Max(totalWidth, desired.X);
                    totalHeight += desired.Y;
                }
                else
                {
                    node.Measure(new Vector2(float.PositiveInfinity, availableSize.Y));
                    var desired = node.DesiredSize;
                    totalWidth += desired.X;
                    totalHeight = Math.Max(totalHeight, desired.Y);
                }
            }
        }

        return new Vector2(totalWidth, totalHeight);
    }

    protected override void ArrangeOverride(Rect arrangeRect)
    {
        float offset = 0f;

        foreach (var child in Children)
        {
            if (child is LayoutNode node)
            {
                var desired = node.DesiredSize;
                
                if (Orientation == Orientation.Vertical)
                {
                    float childHeight = desired.Y;
                    node.Arrange(new Rect(arrangeRect.X, arrangeRect.Y + offset, arrangeRect.Width, childHeight));
                    offset += childHeight;
                }
                else
                {
                    float childWidth = desired.X;
                    node.Arrange(new Rect(arrangeRect.X + offset, arrangeRect.Y, childWidth, arrangeRect.Height));
                    offset += childWidth;
                }
            }
        }
    }
}
