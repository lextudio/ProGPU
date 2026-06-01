using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.UI.Xaml.Shapes;

public class Path : Shape
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(
            "Data",
            typeof(Geometry),
            typeof(Path),
            new PropertyMetadata(null, OnDataChanged) { AffectsMeasure = true, AffectsArrange = true, AffectsRender = true });

    public Geometry? Data
    {
        get => GetValue(DataProperty) as Geometry;
        set => SetValue(DataProperty, value);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var path = (Path)d;
        if (e.OldValue is Geometry oldG)
        {
            oldG.Changed -= path.OnGeometryChanged;
        }
        if (e.NewValue is Geometry newG)
        {
            newG.Changed += path.OnGeometryChanged;
        }
        path.InvalidateMeasure();
        path.Invalidate();
    }

    private void OnGeometryChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        InvalidateMeasure();
        InvalidateArrange();
        Invalidate();
    }

    public override Geometry? DefiningGeometry => Data;
}
