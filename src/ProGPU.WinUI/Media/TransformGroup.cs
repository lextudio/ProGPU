using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Numerics;

namespace Microsoft.UI.Xaml.Media;

public class TransformGroup : Transform
{
    private readonly HashSet<Transform> _subscribedChildren = new();

    private void EnsureSubscribed(Transform child)
    {
        if (child != null && _subscribedChildren.Add(child))
        {
            child.Changed += OnSubObjectChanged;
        }
    }

    private void OnSubObjectChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        OnPropertyChanged(ChildrenProperty, this, this);
    }

    public static readonly DependencyProperty ChildrenProperty =
        DependencyProperty.Register(
            "Children",
            typeof(List<Transform>),
            typeof(TransformGroup),
            new PropertyMetadata(null) { AffectsMeasure = true, AffectsArrange = true, AffectsRender = true });

    public new List<Transform> Children
    {
        get
        {
            var list = GetValue(ChildrenProperty) as List<Transform>;
            if (list == null)
            {
                list = new List<Transform>();
                SetValue(ChildrenProperty, list);
            }
            return list;
        }
    }

    public override Matrix4x4 Value
    {
        get
        {
            var result = Matrix4x4.Identity;
            foreach (var child in Children)
            {
                if (child != null)
                {
                    EnsureSubscribed(child);
                    result *= child.Value;
                }
            }
            return result;
        }
    }
}
