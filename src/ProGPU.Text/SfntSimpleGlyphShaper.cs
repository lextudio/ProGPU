using System;
using System.Collections.Generic;

namespace ProGPU.Text;

#pragma warning disable IDE0078, IDE0305

#if PROGPU_TEXT_PUBLIC
public
#else
internal
#endif
readonly struct SfntSimpleGlyphRun
{
    public SfntSimpleGlyphRun(ushort[] clusterMap, ushort[] glyphIndices)
    {
        ArgumentNullException.ThrowIfNull(clusterMap);
        ArgumentNullException.ThrowIfNull(glyphIndices);

        ClusterMap = clusterMap;
        GlyphIndices = glyphIndices;
    }

    public ushort[] ClusterMap { get; }

    public ushort[] GlyphIndices { get; }
}

#if PROGPU_TEXT_PUBLIC
public
#else
internal
#endif
readonly struct SfntSimpleGlyphMetrics
{
    public SfntSimpleGlyphMetrics(uint advanceWidth, uint advanceHeight)
    {
        AdvanceWidth = advanceWidth;
        AdvanceHeight = advanceHeight;
    }

    public uint AdvanceWidth { get; }

    public uint AdvanceHeight { get; }
}

#if PROGPU_TEXT_PUBLIC
public
#else
internal
#endif
static class SfntSimpleGlyphShaper
{
    private const uint SoftHyphen = 0x00AD;

    public static SfntSimpleGlyphRun CreateGlyphRun(
        ReadOnlySpan<char> text,
        Func<uint, ushort> getGlyphIndex,
        ushort blankGlyphIndex,
        ushort hyphenGlyphIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(getGlyphIndex);

        var clusterMap = new ushort[text.Length];
        var glyphIndices = new List<ushort>(text.Length);

        var textIndex = 0;
        while (textIndex < text.Length)
        {
            var glyphCluster = checked((ushort)glyphIndices.Count);
            uint codePoint = ReadCodePoint(text, textIndex, out int codeUnitCount);
            ushort glyphIndex = GetSimpleGlyphIndex(getGlyphIndex, codePoint, blankGlyphIndex, hyphenGlyphIndex);
            glyphIndices.Add(glyphIndex);

            for (var i = 0; i < codeUnitCount; i++)
            {
                clusterMap[textIndex + i] = glyphCluster;
            }

            textIndex += codeUnitCount;
        }

        return new SfntSimpleGlyphRun(clusterMap, glyphIndices.ToArray());
    }

    public static void FillGlyphAdvances(
        ReadOnlySpan<char> text,
        ReadOnlySpan<ushort> clusterMap,
        ReadOnlySpan<ushort> glyphIndices,
        Func<ushort, SfntSimpleGlyphMetrics> getGlyphMetrics,
        ushort designUnitsPerEm,
        double fontEmSize,
        double scalingFactor,
        bool isSideways,
        Span<int> glyphAdvances)
    {
        ArgumentNullException.ThrowIfNull(getGlyphMetrics);

        if (clusterMap.Length < text.Length)
        {
            throw new ArgumentException("Cluster map length must cover the text length.", nameof(clusterMap));
        }

        if (glyphAdvances.Length < glyphIndices.Length)
        {
            throw new ArgumentException("Glyph advance span length must cover the glyph index count.", nameof(glyphAdvances));
        }

        var unitsPerEm = designUnitsPerEm == 0 ? 1 : designUnitsPerEm;

        for (var i = 0; i < glyphIndices.Length; i++)
        {
            var advance = 0;
            if (!IsControlGlyph(text, clusterMap, i))
            {
                SfntSimpleGlyphMetrics metrics = getGlyphMetrics(glyphIndices[i]);
                uint designAdvance = isSideways ? metrics.AdvanceHeight : metrics.AdvanceWidth;
                advance = checked((int)Math.Round(designAdvance * fontEmSize * scalingFactor / unitsPerEm));
            }

            glyphAdvances[i] = advance;
        }
    }

    public static uint ReadCodePoint(ReadOnlySpan<char> text, int textIndex, out int codeUnitCount)
    {
        if ((uint)textIndex >= (uint)text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(textIndex));
        }

        char current = text[textIndex];
        if (char.IsHighSurrogate(current) &&
            textIndex + 1 < text.Length &&
            char.IsLowSurrogate(text[textIndex + 1]))
        {
            codeUnitCount = 2;
            return checked((uint)char.ConvertToUtf32(current, text[textIndex + 1]));
        }

        codeUnitCount = 1;
        return current;
    }

    public static bool IsFormattingControl(uint codePoint)
    {
        return codePoint < 0x20 || (codePoint >= 0x7F && codePoint <= 0x9F);
    }

    private static ushort GetSimpleGlyphIndex(
        Func<uint, ushort> getGlyphIndex,
        uint codePoint,
        ushort blankGlyphIndex,
        ushort hyphenGlyphIndex)
    {
        if (codePoint == SoftHyphen)
        {
            return hyphenGlyphIndex != 0 ? hyphenGlyphIndex : blankGlyphIndex;
        }

        if (IsFormattingControl(codePoint))
        {
            return blankGlyphIndex;
        }

        return getGlyphIndex(codePoint);
    }

    private static bool IsControlGlyph(ReadOnlySpan<char> text, ReadOnlySpan<ushort> clusterMap, int glyphIndex)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (clusterMap[i] == glyphIndex)
            {
                uint codePoint = ReadCodePoint(text, i, out _);
                return IsFormattingControl(codePoint);
            }
        }

        return false;
    }
}
