using System;

namespace System.Drawing;

public class TextureBrush : Brush
{
    public Image Image { get; }

    public TextureBrush(Image image)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
    }

    public override ProGPU.Vector.Brush ToProGpuBrush()
    {
        throw new NotSupportedException("TextureBrush cannot be converted to a vector brush; use a texture-aware Graphics fill path.");
    }
}
