using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ProGPU.Layout;
using ProGPU.Scene;

namespace Microsoft.UI.Xaml.Controls;

public class CanvasPositionInfo
{
    public float Left { get; set; }
    public float Top { get; set; }
}

public class Canvas : Panel
{
    private static readonly ConditionalWeakTable<Visual, CanvasPositionInfo> _posInfo = new();

    public static void SetLeft(Visual child, float left)
    {
        var info = _posInfo.GetOrCreateValue(child);
        info.Left = left;
        if (child.Parent is Canvas canvas)
        {
            canvas.Invalidate();
        }
    }

    public static void SetTop(Visual child, float top)
    {
        var info = _posInfo.GetOrCreateValue(child);
        info.Top = top;
        if (child.Parent is Canvas canvas)
        {
            canvas.Invalidate();
        }
    }

    public static float GetLeft(Visual child)
    {
        if (_posInfo.TryGetValue(child, out var info))
        {
            return info.Left;
        }
        return 0f;
    }

    public static float GetTop(Visual child)
    {
        if (_posInfo.TryGetValue(child, out var info))
        {
            return info.Top;
        }
        return 0f;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children)
        {
            if (child is LayoutNode node)
            {
                node.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
            }
        }
        return Vector2.Zero;
    }

    protected override void ArrangeOverride(Rect arrangeRect)
    {
        foreach (var child in Children)
        {
            if (child is LayoutNode node)
            {
                float left = GetLeft(node);
                float top = GetTop(node);
                node.Arrange(new Rect(arrangeRect.X + left, arrangeRect.Y + top, node.DesiredSize.X, node.DesiredSize.Y));
            }
        }
    }
}
