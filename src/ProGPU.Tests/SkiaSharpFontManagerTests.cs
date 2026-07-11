using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public class SkiaSharpFontManagerTests
{
    private const int HanCodepoint = 0x5203;

    [Fact]
    public void JapaneseLanguagePrioritizesJapaneseSansFamilies()
    {
        IReadOnlyList<string> families = SKFontManager.GetFallbackFamilyPreferences(
            new[] { "ja-JP" },
            HanCodepoint);

        Assert.Equal("Hiragino Sans", families[0]);
        Assert.True(IndexOf(families, "Noto Sans CJK JP") < IndexOf(families, "PingFang SC"));
    }

    [Fact]
    public void TraditionalChineseLanguagePrecedesDefaultHanFallback()
    {
        IReadOnlyList<string> families = SKFontManager.GetFallbackFamilyPreferences(
            new[] { "zh_HANT" },
            HanCodepoint);

        Assert.Equal("PingFang TC", families[0]);
        Assert.True(IndexOf(families, "Heiti TC") < IndexOf(families, "Heiti SC"));
    }

    [Fact]
    public void LanguageListPreservesCallerPreferenceOrder()
    {
        IReadOnlyList<string> families = SKFontManager.GetFallbackFamilyPreferences(
            new[] { "ko", "ja" },
            HanCodepoint);

        Assert.True(IndexOf(families, "Apple SD Gothic Neo") < IndexOf(families, "Hiragino Sans"));
    }

    [Fact]
    public void HanCodepointUsesSimplifiedChineseDefaultWithoutLanguage()
    {
        IReadOnlyList<string> families = SKFontManager.GetFallbackFamilyPreferences(
            Array.Empty<string>(),
            HanCodepoint);

        Assert.Equal("PingFang SC", families[0]);
        Assert.Contains("Noto Sans CJK SC", families);
    }

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}
