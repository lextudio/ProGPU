namespace ProGPU.Text.Shaping;

/// <summary>Convenience accessors that keep the engine's call sites in font-design terms.</summary>
internal static class ShapingFontFaceExtensions
{
    public static ushort GetGlyphIndex(this IShapingFontFace font, uint codePoint) =>
        (ushort)Math.Min(font.GetNominalGlyph(codePoint), ushort.MaxValue);

    public static bool TryGetTable(this IShapingFontFace font, string tag, out ReadOnlyMemory<byte> table) =>
        font.TryGetTableByName(tag, out table);

    public static float GetAdvanceWidth(this IShapingFontFace font, ushort glyph, float emSize) =>
        font.GetHorizontalAdvanceScaled(glyph, emSize);

    public static float GetAdvanceHeight(this IShapingFontFace font, ushort glyph, float emSize) =>
        font.GetVerticalAdvanceScaled(glyph, emSize);

    public static float GetVerticalOriginY(this IShapingFontFace font, ushort glyph, float emSize) =>
        font.GetVerticalOriginScaled(glyph, emSize);

    public static bool TryGetGlyphBounds(this IShapingFontFace font, ushort glyph,
        out short xMin, out short yMin, out short xMax, out short yMax)
    {
        xMin = yMin = xMax = yMax = 0;
        if (!font.TryGetGlyphExtents(glyph, out ShapingGlyphExtents e)) return false;
        xMin = (short)e.XBearing;
        yMax = (short)e.YBearing;
        xMax = (short)(e.XBearing + e.Width);
        yMin = (short)(e.YBearing + e.Height);
        return true;
    }
}
