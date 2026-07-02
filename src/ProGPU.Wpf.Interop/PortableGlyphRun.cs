using System;

namespace ProGPU.Wpf.Interop;

public interface IPortableGlyphRunSource
{
    bool TryGetPortableGlyphRun(out PortableGlyphRun glyphRun);
}

public sealed class PortableGlyphRun
{
    public ushort[] GlyphIndices { get; set; } = Array.Empty<ushort>();

    public PortablePoint[] GlyphPositions { get; set; } = Array.Empty<PortablePoint>();

    public double[] AdvanceWidths { get; set; } = Array.Empty<double>();

    public PortablePoint[] GlyphOffsets { get; set; } = Array.Empty<PortablePoint>();

    public PortablePoint BaselineOrigin { get; set; }

    public double FontRenderingEmSize { get; set; }

    public object? NativeFont { get; set; }

    public string? FontUri { get; set; }

    public string[] FontFamilyNames { get; set; } = Array.Empty<string>();

    public bool IsBold { get; set; }

    public bool IsItalic { get; set; }

    public bool HasTransform { get; set; }

    public PortableMatrix3x2 Transform { get; set; } = PortableMatrix3x2.Identity;
}
