using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkSurfacePropertiesCompatibilityTests
{
    [Fact]
    public void PixelGeometryValuesMatchSkiaSharp148()
    {
        Assert.Equal(0, (int)SKPixelGeometry.Unknown);
        Assert.Equal(1, (int)SKPixelGeometry.RgbHorizontal);
        Assert.Equal(2, (int)SKPixelGeometry.BgrHorizontal);
        Assert.Equal(3, (int)SKPixelGeometry.RgbVertical);
        Assert.Equal(4, (int)SKPixelGeometry.BgrVertical);
    }

    [Fact]
    public void ConstructorsRetainFlagsAndPixelGeometry()
    {
        using var defaults = new SKSurfaceProperties(SKPixelGeometry.RgbHorizontal);
        Assert.Equal(SKSurfacePropsFlags.None, defaults.Flags);
        Assert.Equal(SKPixelGeometry.RgbHorizontal, defaults.PixelGeometry);
        Assert.False(defaults.IsUseDeviceIndependentFonts);

        using var typed = new SKSurfaceProperties(
            SKSurfacePropsFlags.UseDeviceIndependentFonts,
            SKPixelGeometry.BgrVertical);
        Assert.Equal(SKSurfacePropsFlags.UseDeviceIndependentFonts, typed.Flags);
        Assert.Equal(SKPixelGeometry.BgrVertical, typed.PixelGeometry);
        Assert.True(typed.IsUseDeviceIndependentFonts);
    }

    [Fact]
    public void UIntConstructorPreservesUnknownFlagBits()
    {
        const uint flags = 0x80000001u;
        using var properties = new SKSurfaceProperties(flags, SKPixelGeometry.RgbVertical);

        Assert.Equal(flags, unchecked((uint)properties.Flags));
        Assert.True(properties.IsUseDeviceIndependentFonts);
        Assert.Equal(SKPixelGeometry.RgbVertical, properties.PixelGeometry);
    }
}
