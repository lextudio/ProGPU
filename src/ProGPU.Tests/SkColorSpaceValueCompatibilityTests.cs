using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkColorSpaceValueCompatibilityTests
{
    [Fact]
    public void NamedTransferFunctionsUseNativeMarkersAndFastEvaluation()
    {
        Assert.Equal(
            new[] { 2.4f, 0.9478673f, 0.0521327f, 0.07739938f, 0.04045f, 0f, 0f },
            SKColorSpaceTransferFn.Srgb.Values);
        Assert.Equal(new[] { -5f, 203f, 0f, 0f, 0f, 0f, 0f }, SKColorSpaceTransferFn.Pq.Values);
        Assert.Equal(new[] { -6f, 203f, 1000f, 1.2f, 0f, 0f, 0f }, SKColorSpaceTransferFn.Hlg.Values);

        AssertNear(-0.05086398f, SKColorSpaceTransferFn.Srgb.Transform(-0.25f));
        AssertNear(0.21399307f, SKColorSpaceTransferFn.Srgb.Transform(0.5f));
        AssertNear(0.5000076f, SKColorSpaceTransferFn.Linear.Transform(0.5f));
        AssertNear(0.00922668f, SKColorSpaceTransferFn.Pq.Transform(0.5f));
        AssertNear(-0.020833334f, SKColorSpaceTransferFn.Hlg.Transform(-0.25f));
        AssertNear(1.0000001f, SKColorSpaceTransferFn.Hlg.Transform(1f));
    }

    [Fact]
    public void TransferInversionMatchesNativeParametersAndUnsupportedHdrResult()
    {
        var inverse = SKColorSpaceTransferFn.Srgb.Invert();

        AssertNear(0.41666666f, inverse.G);
        AssertNear(1.1372833f, inverse.A);
        AssertNear(12.92f, inverse.C);
        AssertNear(0.003130805f, inverse.D);
        AssertNear(-0.054969788f, inverse.E);
        AssertNear(0.5f, inverse.Transform(SKColorSpaceTransferFn.Srgb.Transform(0.5f)), 0.0002f);
        Assert.Equal(SKColorSpaceTransferFn.Empty, SKColorSpaceTransferFn.Pq.Invert());
        Assert.Equal(SKColorSpaceTransferFn.Empty, SKColorSpaceTransferFn.Hlg.Invert());
        Assert.Throws<ArgumentException>(() => new SKColorSpaceTransferFn(new float[6]));
    }

    [Fact]
    public void XyzMatricesKeepInlineValueOwnershipAndNativeIndexing()
    {
        var source = new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 10f };
        var matrix = new SKColorSpaceXyz(source);
        source[0] = 99f;

        Assert.Equal(1f, matrix[0, 0]);
        Assert.Equal(6f, matrix[2, 1]);
        Assert.Equal(8f, matrix[1, 2]);
        var values = matrix.Values;
        values[0] = 42f;
        Assert.Equal(1f, matrix[0, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = matrix[-1, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = matrix[0, 3]);

        matrix.Values = new[] { 2f, 0f, 1f, 1f, 3f, 0f, 4f, 1f, 2f };
        Assert.Equal(new SKColorSpaceXyz(2f, 0f, 1f, 1f, 3f, 0f, 4f, 1f, 2f), matrix);
        Assert.Equal(Enumerable.Repeat(0.25f, 9), new SKColorSpaceXyz(0.25f).Values);
    }

    [Fact]
    public void XyzInversionAndConcatenationMatchNativeRowMajorMath()
    {
        var left = new SKColorSpaceXyz(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 10f);
        var right = new SKColorSpaceXyz(2f, 0f, 1f, 1f, 3f, 0f, 4f, 1f, 2f);

        Assert.Equal(
            new SKColorSpaceXyz(16f, 9f, 7f, 37f, 21f, 16f, 62f, 34f, 27f),
            SKColorSpaceXyz.Concat(left, right));
        Assert.Equal(SKColorSpaceXyz.Empty, new SKColorSpaceXyz(1f).Invert());

        var srgbInverse = SKColorSpaceXyz.Srgb.Invert().Values;
        AssertNear(3.1341121f, srgbInverse[0]);
        AssertNear(-1.6173924f, srgbInverse[1]);
        AssertNear(-0.4906334f, srgbInverse[2]);
        AssertNear(1.4053851f, srgbInverse[8]);
        Assert.Equal(int.MinValue, BitConverter.SingleToInt32Bits(SKColorSpaceXyz.Identity.Invert().Values[7]));
        Assert.Equal(SKColorSpaceXyz.Identity, SKColorSpaceXyz.Xyz);
    }

    private static void AssertNear(float expected, float actual, float tolerance = 0.000001f) =>
        Assert.InRange(actual, expected - tolerance, expected + tolerance);
}
