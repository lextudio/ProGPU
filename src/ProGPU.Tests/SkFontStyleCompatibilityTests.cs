using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkFontStyleCompatibilityTests
{
    [Fact]
    public void DefaultConstructorUsesNormalStyle()
    {
        using var style = new SKFontStyle();

        Assert.Equal((int)SKFontStyleWeight.Normal, style.Weight);
        Assert.Equal((int)SKFontStyleWidth.Normal, style.Width);
        Assert.Equal(SKFontStyleSlant.Upright, style.Slant);
    }

    [Fact]
    public void IntegerConstructorPreservesCallerValues()
    {
        using var style = new SKFontStyle(575, 11, SKFontStyleSlant.Oblique);

        Assert.Equal(575, style.Weight);
        Assert.Equal(11, style.Width);
        Assert.Equal(SKFontStyleSlant.Oblique, style.Slant);
    }

    [Fact]
    public void StaticStylesAreStableAndMatchNativeValues()
    {
        Assert.Same(SKFontStyle.Normal, SKFontStyle.Normal);
        Assert.Same(SKFontStyle.Bold, SKFontStyle.Bold);
        Assert.Same(SKFontStyle.Italic, SKFontStyle.Italic);
        Assert.Same(SKFontStyle.BoldItalic, SKFontStyle.BoldItalic);

        AssertStyle(SKFontStyle.Normal, SKFontStyleWeight.Normal, SKFontStyleSlant.Upright);
        AssertStyle(SKFontStyle.Bold, SKFontStyleWeight.Bold, SKFontStyleSlant.Upright);
        AssertStyle(SKFontStyle.Italic, SKFontStyleWeight.Normal, SKFontStyleSlant.Italic);
        AssertStyle(SKFontStyle.BoldItalic, SKFontStyleWeight.Bold, SKFontStyleSlant.Italic);
    }

    [Fact]
    public void StaticStylesIgnorePublicDisposalWhileOwnedStylesReleaseTheirHandle()
    {
        var sharedHandle = SKFontStyle.Normal.Handle;
        SKFontStyle.Normal.Dispose();
        Assert.Equal(sharedHandle, SKFontStyle.Normal.Handle);

        var owned = new SKFontStyle();
        Assert.NotEqual(IntPtr.Zero, owned.Handle);
        owned.Dispose();
        Assert.Equal(IntPtr.Zero, owned.Handle);
    }

    private static void AssertStyle(SKFontStyle style, SKFontStyleWeight weight, SKFontStyleSlant slant)
    {
        Assert.Equal((int)weight, style.Weight);
        Assert.Equal((int)SKFontStyleWidth.Normal, style.Width);
        Assert.Equal(slant, style.Slant);
    }
}
