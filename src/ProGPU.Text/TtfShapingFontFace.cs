using ProGPU.Text.Shaping;

namespace ProGPU.Text;

/// <summary>
/// Adapts the existing parsed <see cref="TtfFont"/> to the standalone shaping
/// package. It performs no graphics initialization and borrows table memory from
/// the immutable font for the adapter's lifetime.
/// </summary>
public sealed class TtfShapingFontFace : IShapingFontFace
{
    public TtfShapingFontFace(TtfFont font)
    {
        Font = font ?? throw new ArgumentNullException(nameof(font));
    }

    public TtfFont Font { get; }
    public int FaceIndex => Font.FaceIndex;
    public ushort UnitsPerEm => Font.UnitsPerEm;
    public uint GlyphCount => Font.NumGlyphs;
    public uint VariationAxisCount => checked((uint)Font.VariationAxes.Count);
    public bool HasActiveVariations => Font.HasActiveFontVariations;

    public bool TryGetTable(OpenTypeTag tag, out ReadOnlyMemory<byte> table) =>
        Font.TryGetTable(tag.ToString(), out table);

    public uint GetNominalGlyph(uint codePoint) => Font.GetGlyphIndex(codePoint);

    public bool TryGetVariationGlyph(uint codePoint, uint variationSelector, out uint glyphId)
    {
        bool found = Font.TryGetVariationGlyph(codePoint, variationSelector, out ushort glyph);
        glyphId = glyph;
        return found;
    }

    public int GetHorizontalAdvance(uint glyphId)
    {
        if (glyphId > ushort.MaxValue) return 0;
        return checked((int)MathF.Round(
            Font.GetAdvanceWidth((ushort)glyphId, Font.UnitsPerEm),
            MidpointRounding.AwayFromZero));
    }

    public int GetVerticalAdvance(uint glyphId)
    {
        if (glyphId > ushort.MaxValue) return 0;
        return checked((int)MathF.Round(Font.GetAdvanceHeight((ushort)glyphId, Font.UnitsPerEm)));
    }

    public int GetHorizontalOrigin(uint glyphId) => GetHorizontalAdvance(glyphId) / 2;

    public int GetVerticalOrigin(uint glyphId)
    {
        if (glyphId > ushort.MaxValue) return 0;
        return checked((int)MathF.Round(Font.GetVerticalOriginY((ushort)glyphId, Font.UnitsPerEm)));
    }

    public bool TryGetGlyphExtents(uint glyphId, out ShapingGlyphExtents extents)
    {
        if (glyphId <= ushort.MaxValue && Font.TryGetGlyphBounds(
                (ushort)glyphId, out short xMin, out short yMin, out short xMax, out short yMax))
        {
            extents = new ShapingGlyphExtents(xMin, yMax, xMax - xMin, yMin - yMax);
            return true;
        }
        extents = default;
        return false;
    }

    public bool TryGetNormalizedVariationCoordinate(uint axisIndex, out short coordinate)
    {
        coordinate = 0;
        return axisIndex <= ushort.MaxValue &&
            Font.TryGetNormalizedVariationCoordinate((ushort)axisIndex, out coordinate);
    }

    public float GetLayoutVariationDelta(ushort outerIndex, ushort innerIndex) =>
        Font.GetLayoutVariationDelta(outerIndex, innerIndex);

    public bool HasGlyphOutline(uint glyphId) =>
        glyphId <= ushort.MaxValue && Font.GetFlippedGlyphOutline((ushort)glyphId) is not null;

    public float GetHorizontalAdvanceScaled(uint glyphId, float emSize) =>
        glyphId > ushort.MaxValue ? 0f : Font.GetAdvanceWidth((ushort)glyphId, emSize);

    public float GetVerticalAdvanceScaled(uint glyphId, float emSize) =>
        glyphId > ushort.MaxValue ? 0f : Font.GetAdvanceHeight((ushort)glyphId, emSize);

    public float GetVerticalOriginScaled(uint glyphId, float emSize) =>
        glyphId > ushort.MaxValue ? 0f : Font.GetVerticalOriginY((ushort)glyphId, emSize);

    public bool TryGetTableByName(string tag, out ReadOnlyMemory<byte> table) =>
        Font.TryGetTable(tag, out table);
}
