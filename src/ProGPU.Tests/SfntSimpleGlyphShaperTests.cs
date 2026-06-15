using ProGPU.Text;
using Xunit;

namespace ProGPU.Tests;

public class SfntSimpleGlyphShaperTests
{
    [Fact]
    public void CreateGlyphRunPreservesSurrogateClusters()
    {
        string text = "A" + char.ConvertFromUtf32(0x1F600) + "B";

        SfntSimpleGlyphRun run = SfntSimpleGlyphShaper.CreateGlyphRun(
            text.AsSpan(),
            codePoint => codePoint switch
            {
                'A' => 1,
                0x1F600 => 2,
                'B' => 3,
                _ => 0
            },
            blankGlyphIndex: 99);

        Assert.Equal(new ushort[] { 0, 1, 1, 2 }, run.ClusterMap);
        Assert.Equal(new ushort[] { 1, 2, 3 }, run.GlyphIndices);
    }

    [Fact]
    public void CreateGlyphRunMapsSoftHyphenAndFormattingControls()
    {
        string text = "\u00AD\nA";

        SfntSimpleGlyphRun run = SfntSimpleGlyphShaper.CreateGlyphRun(
            text.AsSpan(),
            codePoint => codePoint == 'A' ? (ushort)1 : (ushort)0,
            blankGlyphIndex: 99,
            hyphenGlyphIndex: 7);

        Assert.Equal(new ushort[] { 0, 1, 2 }, run.ClusterMap);
        Assert.Equal(new ushort[] { 7, 99, 1 }, run.GlyphIndices);
    }

    [Fact]
    public void FillGlyphAdvancesSuppressesControlsAndUsesSidewaysMetrics()
    {
        string text = "A\nB";
        ushort[] clusterMap = { 0, 1, 2 };
        ushort[] glyphIndices = { 10, 99, 11 };
        int[] advances = new int[glyphIndices.Length];

        SfntSimpleGlyphShaper.FillGlyphAdvances(
            text.AsSpan(),
            clusterMap,
            glyphIndices,
            glyphIndex => glyphIndex == 10
                ? new SfntSimpleGlyphMetrics(500, 700)
                : new SfntSimpleGlyphMetrics(600, 800),
            designUnitsPerEm: 1000,
            fontEmSize: 10,
            scalingFactor: 1,
            isSideways: true,
            advances);

        Assert.Equal(new[] { 7, 0, 8 }, advances);
    }
}
