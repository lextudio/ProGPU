using System.Numerics;

namespace System.Windows.Media;

public class SolidColorBrush : Brush
{
    private Color _color;

    public Color Color
    {
        get => _color;
        set
        {
            if (Equals(_color, value))
            {
                return;
            }

            _color = value;
            OnChanged();
        }
    }

    public SolidColorBrush() { }

    public SolidColorBrush(Color color)
    {
        _color = color;
    }

    public override ProGPU.Vector.Brush ToNative()
    {
        return new ProGPU.Vector.SolidColorBrush(new Vector4(Color.R / 255f, Color.G / 255f, Color.B / 255f, Color.A / 255f))
        {
            Opacity = (float)Math.Clamp(Opacity, 0.0, 1.0)
        };
    }
}
