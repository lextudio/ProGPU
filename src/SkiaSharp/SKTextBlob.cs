using System;

namespace SkiaSharp;

public class SKTextBlob : IDisposable
{
    public SKFont Font { get; }
    public ushort[] GlyphIndices { get; }
    public SKPoint[] GlyphPositions { get; }

    public SKTextBlob(SKFont font, ushort[] glyphIndices, SKPoint[] glyphPositions)
    {
        Font = font;
        GlyphIndices = glyphIndices;
        GlyphPositions = glyphPositions;
    }

    public float[] GetIntercepts(float lowerLimit, float upperLimit)
    {
        // Intercepts are used for text underline/strikeout intersections; we return an empty array for basic support.
        return Array.Empty<float>();
    }

    public void Dispose() { }
}

public class PositionedRunBuffer
{
    public SKFont Font { get; }
    public ushort[] Glyphs { get; }
    public SKPoint[] Positions { get; }

    public PositionedRunBuffer(SKFont font, int count)
    {
        Font = font;
        Glyphs = new ushort[count];
        Positions = new SKPoint[count];
    }

    public void SetPositions(ReadOnlySpan<SKPoint> positions)
    {
        positions.CopyTo(Positions);
    }

    public void SetPositions(SKPoint[] positions)
    {
        Array.Copy(positions, Positions, Positions.Length);
    }

    public void SetGlyphs(ReadOnlySpan<ushort> glyphs)
    {
        glyphs.CopyTo(Glyphs);
    }

    public void SetGlyphs(ushort[] glyphs)
    {
        Array.Copy(glyphs, Glyphs, Glyphs.Length);
    }
}

public class SKTextBlobBuilder : IDisposable
{
    private PositionedRunBuffer? _activeRun;

    public PositionedRunBuffer AllocatePositionedRun(SKFont font, int count)
    {
        _activeRun = new PositionedRunBuffer(font, count);
        return _activeRun;
    }

    public SKTextBlob? Build()
    {
        if (_activeRun == null) return null;
        var blob = new SKTextBlob(_activeRun.Font, _activeRun.Glyphs, _activeRun.Positions);
        _activeRun = null;
        return blob;
    }

    public void Dispose() { }
}

public class SKTextBlobBuilderCache
{
    private static readonly SKTextBlobBuilderCache _shared = new();
    public static SKTextBlobBuilderCache Shared => _shared;

    public SKTextBlobBuilder Get() => new();
    public void Return(SKTextBlobBuilder builder) => builder.Dispose();
}
