using System;
using System.Numerics;

namespace ProGPU.Vector;

public struct Color
{
    public byte A { get; set; }
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static Color FromArgb(byte a, byte r, byte g, byte b)
    {
        return new Color(r, g, b, a);
    }

    public static implicit operator Vector4(Color c)
    {
        return new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
    }
}

