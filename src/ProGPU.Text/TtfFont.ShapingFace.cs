using ProGPU.Text.Shaping;

namespace ProGPU.Text;

// The OpenType shaping engine lives in ProGPU.Text.Shaping and consumes IShapingFontFace.
// TtfFont implements it directly so the engine's per-font plan cache is keyed by the font
// itself (no per-call adapter allocation) and existing OpenTypeTextShaper.Shape(text, font, ...)
// call sites bind unchanged.
public partial class TtfFont : IShapingFontFace
{
    uint IShapingFontFace.GlyphCount => NumGlyphs;
    bool IShapingFontFace.HasActiveVariations => HasActiveVariations;
    uint IShapingFontFace.VariationAxisCount => checked((uint)VariationAxes.Count);

    bool IShapingFontFace.TryGetTable(OpenTypeTag tag, out ReadOnlyMemory<byte> table) =>
        TryGetTable(tag.ToString(), out table);

    uint IShapingFontFace.GetNominalGlyph(uint codePoint) => GetGlyphIndex(codePoint);

    bool IShapingFontFace.TryGetVariationGlyph(uint codePoint, uint variationSelector, out uint glyphId)
    {
        bool found = TryGetVariationGlyph(codePoint, variationSelector, out ushort glyph);
        glyphId = glyph;
        return found;
    }

    int IShapingFontFace.GetHorizontalAdvance(uint glyphId) =>
        glyphId > ushort.MaxValue ? 0
            : checked((int)MathF.Round(GetAdvanceWidth((ushort)glyphId, UnitsPerEm), MidpointRounding.AwayFromZero));

    int IShapingFontFace.GetVerticalAdvance(uint glyphId) =>
        glyphId > ushort.MaxValue ? 0
            : checked((int)MathF.Round(GetAdvanceHeight((ushort)glyphId, UnitsPerEm)));

    int IShapingFontFace.GetHorizontalOrigin(uint glyphId) =>
        ((IShapingFontFace)this).GetHorizontalAdvance(glyphId) / 2;

    int IShapingFontFace.GetVerticalOrigin(uint glyphId) =>
        glyphId > ushort.MaxValue ? 0
            : checked((int)MathF.Round(GetVerticalOriginY((ushort)glyphId, UnitsPerEm)));

    bool IShapingFontFace.TryGetGlyphExtents(uint glyphId, out ShapingGlyphExtents extents)
    {
        if (glyphId <= ushort.MaxValue &&
            TryGetGlyphBounds((ushort)glyphId, out short xMin, out short yMin, out short xMax, out short yMax))
        {
            extents = new ShapingGlyphExtents(xMin, yMax, xMax - xMin, yMin - yMax);
            return true;
        }
        extents = default;
        return false;
    }

    bool IShapingFontFace.TryGetNormalizedVariationCoordinate(uint axisIndex, out short coordinate)
    {
        coordinate = 0;
        return axisIndex <= ushort.MaxValue &&
            TryGetNormalizedVariationCoordinate((ushort)axisIndex, out coordinate);
    }

    float IShapingFontFace.GetLayoutVariationDelta(ushort outerIndex, ushort innerIndex) =>
        GetLayoutVariationDelta(outerIndex, innerIndex);

    bool IShapingFontFace.HasGlyphOutline(uint glyphId) =>
        glyphId <= ushort.MaxValue && GetFlippedGlyphOutline((ushort)glyphId) is not null;

    float IShapingFontFace.GetHorizontalAdvanceScaled(uint glyphId, float emSize) =>
        glyphId > ushort.MaxValue ? 0f : GetAdvanceWidth((ushort)glyphId, emSize);

    float IShapingFontFace.GetVerticalAdvanceScaled(uint glyphId, float emSize) =>
        glyphId > ushort.MaxValue ? 0f : GetAdvanceHeight((ushort)glyphId, emSize);

    float IShapingFontFace.GetVerticalOriginScaled(uint glyphId, float emSize) =>
        glyphId > ushort.MaxValue ? 0f : GetVerticalOriginY((ushort)glyphId, emSize);

    bool IShapingFontFace.TryGetTableByName(string tag, out ReadOnlyMemory<byte> table) =>
        TryGetTable(tag, out table);
}
