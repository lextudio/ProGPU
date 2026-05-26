using System;

namespace ProGPU.Vector;

public static class Colors
{
    public static Color Transparent => new Color(0, 0, 0, 0);
    public static Color Black => new Color(0, 0, 0, 255);
    public static Color White => new Color(255, 255, 255, 255);
    public static Color Red => new Color(255, 0, 0, 255);
    public static Color Green => new Color(0, 255, 0, 255);
    public static Color Blue => new Color(0, 0, 255, 255);

    public static Color FromARGB(byte a, byte r, byte g, byte b) => new Color(r, g, b, a);
    public static Color FromArgb(byte a, byte r, byte g, byte b) => new Color(r, g, b, a);
}

