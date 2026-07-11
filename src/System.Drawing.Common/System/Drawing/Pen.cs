namespace System.Drawing;

public class Pen : IDisposable
{
    private static readonly double[] s_dashPattern = { 3.0, 1.0 };
    private static readonly double[] s_dotPattern = { 1.0, 1.0 };
    private static readonly double[] s_dashDotPattern = { 3.0, 1.0, 1.0, 1.0 };
    private static readonly double[] s_dashDotDotPattern = { 3.0, 1.0, 1.0, 1.0, 1.0, 1.0 };
    private static readonly double[] s_defaultCustomPattern = { 1.0 };

    public Brush Brush { get; set; }
    public float Width { get; set; }
    public System.Drawing.Drawing2D.DashStyle DashStyle { get; set; }
    public float DashOffset { get; set; }

    public Color Color
    {
        get => Brush is SolidBrush solidBrush ? solidBrush.Color : Color.Black;
        set => Brush = new SolidBrush(value);
    }

    public Pen(Color color) : this(color, 1.0f) {}

    public Pen(Color color, float width)
    {
        Brush = new SolidBrush(color);
        Width = width;
    }

    public Pen(Brush brush) : this(brush, 1.0f) {}

    public Pen(Brush brush, float width)
    {
        Brush = brush;
        Width = width;
    }

    public ProGPU.Vector.Pen ToProGpuPen()
    {
        return ToProGpuPen(Width);
    }

    internal ProGPU.Vector.Pen ToProGpuPen(float width)
    {
        return new ProGPU.Vector.Pen(
            Brush.ToProGpuBrush(),
            width,
            dashArray: GetDashArray(DashStyle),
            dashOffset: DashOffset);
    }

    private static double[]? GetDashArray(System.Drawing.Drawing2D.DashStyle dashStyle)
    {
        return dashStyle switch
        {
            System.Drawing.Drawing2D.DashStyle.Dash => s_dashPattern,
            System.Drawing.Drawing2D.DashStyle.Dot => s_dotPattern,
            System.Drawing.Drawing2D.DashStyle.DashDot => s_dashDotPattern,
            System.Drawing.Drawing2D.DashStyle.DashDotDot => s_dashDotDotPattern,
            System.Drawing.Drawing2D.DashStyle.Custom => s_defaultCustomPattern,
            _ => null
        };
    }

    public void Dispose()
    {
        Brush?.Dispose();
    }
}
