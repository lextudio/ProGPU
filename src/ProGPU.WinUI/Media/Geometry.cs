using Microsoft.UI.Xaml;
using ProGPU.Vector;
using ProGPU.Scene;
using System.Numerics;

namespace Microsoft.UI.Xaml.Media;

public abstract class Geometry : DependencyObject
{
    public static readonly DependencyProperty TransformProperty =
        DependencyProperty.Register(
            "Transform",
            typeof(Transform),
            typeof(Geometry),
            new PropertyMetadata(null, OnTransformChanged) { AffectsMeasure = true, AffectsArrange = true, AffectsRender = true });

    public new Transform? Transform
    {
        get => GetValue(TransformProperty) as Transform;
        set => SetValue(TransformProperty, value);
    }

    private static void OnTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var geom = (Geometry)d;
        if (e.OldValue is Transform oldT)
        {
            oldT.Changed -= geom.OnTransformSubChanged;
        }
        if (e.NewValue is Transform newT)
        {
            newT.Changed += geom.OnTransformSubChanged;
        }
    }

    private void OnTransformSubChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        OnPropertyChanged(TransformProperty, this, this);
    }

    internal Matrix4x4? ParentTransformMatrix { get; set; }

    protected Matrix4x4 EffectiveTransform
    {
        get
        {
            Matrix4x4 local = Transform != null ? Transform.Value : Matrix4x4.Identity;
            if (ParentTransformMatrix.HasValue)
            {
                return local * ParentTransformMatrix.Value;
            }
            return local;
        }
    }

    protected bool HasTransform => Transform != null || ParentTransformMatrix.HasValue;

    public abstract void Draw(DrawingContext context, Brush? fill, Pen? pen);

    public abstract Rect Bounds { get; }

    protected Vector2 TransformPoint(Vector2 pt)
    {
        if (HasTransform)
        {
            return Vector2.Transform(pt, EffectiveTransform);
        }
        return pt;
    }
}
