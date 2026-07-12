using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkRotationScaleMatrixCompatibilityTests
{
    [Fact]
    public void SimpleFactoriesMatchNativeComponents()
    {
        Assert.Equal(SKRotationScaleMatrix.Identity, SKRotationScaleMatrix.CreateIdentity());
        Assert.Equal(new SKRotationScaleMatrix(1f, 0f, 7.25f, -3.5f), SKRotationScaleMatrix.CreateTranslation(7.25f, -3.5f));
        Assert.Equal(new SKRotationScaleMatrix(-2.75f, 0f, 0f, 0f), SKRotationScaleMatrix.CreateScale(-2.75f));
    }

    [Fact]
    public void RotationFactoriesPreserveNativeAnchorAndFloatPrecision()
    {
        AssertBits(
            SKRotationScaleMatrix.CreateRotation(0.37f, 12.5f, -4.25f),
            0x3f6ead01,
            0x3eb925a9,
            0xc1530e2a,
            0xbf0ecc1c);
        AssertBits(
            SKRotationScaleMatrix.CreateRotationDegrees(37.25f, 12.5f, -4.25f),
            0x3f4bc6c9,
            0x3f1af48c,
            0xc1485c42,
            0xc085dc80);
    }

    [Fact]
    public void CombinedFactoriesAndMatrixConversionMatchNativeValues()
    {
        var radians = SKRotationScaleMatrix.Create(1.75f, 0.37f, 8.5f, -2.25f, 12.5f, -4.25f);
        AssertBits(radians, 0x3fd0d761, 0x3f2200f4, 0xc16958c9, 0xc04e794a);

        var degrees = SKRotationScaleMatrix.CreateDegrees(1.75f, 37.25f, 8.5f, -2.25f, 12.5f, -4.25f);
        AssertBits(degrees, 0x3fb24df0, 0x3f8795fa, 0xc156a175, 0xc11920f0);

        Assert.Equal(
            new SKMatrix(
                degrees.SCos,
                -degrees.SSin,
                degrees.TX,
                degrees.SSin,
                degrees.SCos,
                degrees.TY,
                0f,
                0f,
                1f),
            degrees.ToMatrix());
    }

    private static void AssertBits(
        SKRotationScaleMatrix actual,
        uint scos,
        uint ssin,
        uint tx,
        uint ty)
    {
        Assert.Equal(unchecked((int)scos), BitConverter.SingleToInt32Bits(actual.SCos));
        Assert.Equal(unchecked((int)ssin), BitConverter.SingleToInt32Bits(actual.SSin));
        Assert.Equal(unchecked((int)tx), BitConverter.SingleToInt32Bits(actual.TX));
        Assert.Equal(unchecked((int)ty), BitConverter.SingleToInt32Bits(actual.TY));
    }
}
