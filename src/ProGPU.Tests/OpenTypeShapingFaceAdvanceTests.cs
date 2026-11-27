using ProGPU.Fonts.Inter;
using ProGPU.Text;
using ProGPU.Text.Shaping;
using Xunit;

namespace ProGPU.Tests;

/// <summary>
/// The shaping engine consumes <see cref="IShapingFontFace"/>. Variable fonts carry fractional
/// HVAR/VVAR advance deltas, so the face's unrounded "scaled" members must feed the engine exactly
/// as the pre-split <c>TtfFont</c> float accessors did.
/// </summary>
public class OpenTypeShapingFaceAdvanceTests
{
    // Hides TtfFont's scaled-advance overrides so only the integer interface members are visible,
    // i.e. what a face without the *Scaled overrides would present to the engine.
    private sealed class IntegerOnlyFace(TtfFont font) : IShapingFontFace
    {
        private readonly IShapingFontFace _inner = font;
        public int FaceIndex => _inner.FaceIndex;
        public ushort UnitsPerEm => _inner.UnitsPerEm;
        public uint GlyphCount => _inner.GlyphCount;
        public uint VariationAxisCount => _inner.VariationAxisCount;
        public bool HasActiveVariations => _inner.HasActiveVariations;
        public bool TryGetTable(OpenTypeTag tag, out ReadOnlyMemory<byte> table) => _inner.TryGetTable(tag, out table);
        public uint GetNominalGlyph(uint codePoint) => _inner.GetNominalGlyph(codePoint);
        public bool TryGetVariationGlyph(uint codePoint, uint selector, out uint glyphId) => _inner.TryGetVariationGlyph(codePoint, selector, out glyphId);
        public int GetHorizontalAdvance(uint glyphId) => _inner.GetHorizontalAdvance(glyphId);
        public int GetVerticalAdvance(uint glyphId) => _inner.GetVerticalAdvance(glyphId);
        public int GetHorizontalOrigin(uint glyphId) => _inner.GetHorizontalOrigin(glyphId);
        public int GetVerticalOrigin(uint glyphId) => _inner.GetVerticalOrigin(glyphId);
        public bool TryGetGlyphExtents(uint glyphId, out ShapingGlyphExtents extents) => _inner.TryGetGlyphExtents(glyphId, out extents);
        public bool TryGetNormalizedVariationCoordinate(uint axisIndex, out short coordinate) => _inner.TryGetNormalizedVariationCoordinate(axisIndex, out coordinate);
        public float GetLayoutVariationDelta(ushort outerIndex, ushort innerIndex) => _inner.GetLayoutVariationDelta(outerIndex, innerIndex);
        public bool HasGlyphOutline(uint glyphId) => _inner.HasGlyphOutline(glyphId);
    }

    public static TheoryData<string> Samples => new()
    {
        "Hamburgefonstiv 0123456789",
        "AV To Wa fi ffl",
        "é ä Å",
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void VariableFontAdvancesMatchBetweenScaledAndIntegerFaces(string text)
    {
        TtfFont font = InterFontFamily.Variable.WithVariations(new FontVariationSetting("wght", 530f));
        Assert.True(font.IsVariableFont);
        Assert.True(((IShapingFontFace)font).HasActiveVariations);

        float size = font.UnitsPerEm; // design units: the engine's native scale
        IReadOnlyList<ShapedGlyph> viaFont = OpenTypeTextShaper.Shape(text, font, size);
        IReadOnlyList<ShapedGlyph> viaInteger = OpenTypeTextShaper.Shape(text, new IntegerOnlyFace(font), size);

        Assert.Equal(viaFont.Count, viaInteger.Count);
        for (int i = 0; i < viaFont.Count; i++)
        {
            Assert.Equal(viaFont[i].GlyphIndex, viaInteger[i].GlyphIndex);
            Assert.Equal(viaFont[i].AdvanceX, viaInteger[i].AdvanceX);
            Assert.Equal(viaFont[i].OffsetX, viaInteger[i].OffsetX);
            Assert.Equal(viaFont[i].OffsetY, viaInteger[i].OffsetY);
        }
    }

    [Fact]
    public void ScaledAdvanceKeepsFractionalVariationDelta()
    {
        TtfFont font = InterFontFamily.Variable.WithVariations(new FontVariationSetting("wght", 530f));
        IShapingFontFace face = font;
        uint glyph = face.GetNominalGlyph('H');
        float exact = font.GetAdvanceWidth((ushort)glyph, 16f);
        Assert.Equal(exact, face.GetHorizontalAdvanceScaled(glyph, 16f));
    }
}
