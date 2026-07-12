using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkColorSpaceCompatibilityTests
{
    [Fact]
    public void XyzConstructorsValuesAndIndexerMatchNative()
    {
        Assert.Equal(Enumerable.Repeat(2f, 9), new SKColorSpaceXyz(2f).Values);
        var matrix = new SKColorSpaceXyz(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f);
        Assert.Equal(new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f }, matrix.Values);
        Assert.Equal(1f, matrix[0, 0]);
        Assert.Equal(3f, matrix[2, 0]);
        Assert.Equal(7f, matrix[0, 2]);
        Assert.Equal(9f, matrix[2, 2]);
    }

    [Fact]
    public void XyzNamedMatricesMatchNative()
    {
        Assert.Equal(new[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f }, SKColorSpaceXyz.Identity.Values);
        Assert.Equal(SKColorSpaceXyz.Identity, SKColorSpaceXyz.Xyz);
        Assert.Equal(0.43606567f, SKColorSpaceXyz.Srgb[0, 0]);
        Assert.Equal(0.6097412f, SKColorSpaceXyz.AdobeRgb[0, 0]);
        Assert.Equal(-0.00104941f, SKColorSpaceXyz.DisplayP3[0, 2]);
        Assert.Equal(0.797162f, SKColorSpaceXyz.Rec2020[2, 2]);
    }

    [Fact]
    public void XyzValuesAreCopiedAndMutableThroughSetter()
    {
        var source = new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f };
        var matrix = new SKColorSpaceXyz(source);
        source[0] = 99f;
        Assert.Equal(1f, matrix[0, 0]);
        var snapshot = matrix.Values;
        snapshot[0] = 88f;
        Assert.Equal(1f, matrix[0, 0]);
        matrix.Values = source;
        Assert.Equal(99f, matrix[0, 0]);
    }

    [Fact]
    public void XyzValidationMatchesNativeExceptions()
    {
        Assert.Equal("values", Assert.Throws<ArgumentNullException>(() => new SKColorSpaceXyz((float[])null!)).ParamName);
        Assert.Equal("values", Assert.Throws<ArgumentException>(() => new SKColorSpaceXyz(new float[8])).ParamName);
        Assert.Throws<NullReferenceException>(() => { var matrix = default(SKColorSpaceXyz); matrix.Values = null!; });
        var value = SKColorSpaceXyz.Identity;
        Assert.Equal("x", Assert.Throws<ArgumentOutOfRangeException>(() => _ = value[-1, 0]).ParamName);
        Assert.Equal("y", Assert.Throws<ArgumentOutOfRangeException>(() => _ = value[0, 3]).ParamName);
    }

    [Fact]
    public void XyzInversionMatchesNative()
    {
        Assert.Equal(
            new[] { -24f, 18f, 5f, 20f, -15f, -4f, -5f, 4f, 1f },
            new SKColorSpaceXyz(1f, 2f, 3f, 0f, 1f, 4f, 5f, 6f, 0f).Invert().Values);
        Assert.Equal(
            new[] { 0.5f, 0f, 0f, 0f, 0.25f, 0f, 0f, -0f, 0.2f },
            new SKColorSpaceXyz(2f, 0f, 0f, 0f, 4f, 0f, 0f, 0f, 5f).Invert().Values);
        Assert.Equal(
            SKColorSpaceXyz.Empty,
            new SKColorSpaceXyz(1f, 2f, 3f, 2f, 4f, 6f, 3f, 6f, 9f).Invert());
    }

    [Fact]
    public void XyzConcatOrderMatchesNative()
    {
        var left = new SKColorSpaceXyz(1f, 2f, 0f, 0f, 1f, 3f, 0f, 0f, 1f);
        var right = new SKColorSpaceXyz(2f, 0f, 0f, 0f, 4f, 0f, 0f, 0f, 5f);
        Assert.Equal(new[] { 2f, 8f, 0f, 0f, 4f, 15f, 0f, 0f, 5f }, SKColorSpaceXyz.Concat(left, right).Values);
        Assert.Equal(new[] { 2f, 4f, 0f, 0f, 4f, 12f, 0f, 0f, 5f }, SKColorSpaceXyz.Concat(right, left).Values);
    }

    [Fact]
    public void XyzEqualityAndHashUseEveryScalar()
    {
        var value = new SKColorSpaceXyz(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f);
        var equal = new SKColorSpaceXyz(value.Values);
        Assert.True(value == equal);
        Assert.False(value != equal);
        Assert.Equal(value.GetHashCode(), equal.GetHashCode());
        var changed = equal;
        changed.Values = new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 10f };
        Assert.NotEqual(value, changed);
    }

    [Fact]
    public void TransferConstructorsValuesAndPropertiesMatchNative()
    {
        var value = new SKColorSpaceTransferFn(new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f });
        Assert.Equal(new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f }, value.Values);
        value.G = 7f;
        value.A = 6f;
        value.B = 5f;
        value.C = 4f;
        value.D = 3f;
        value.E = 2f;
        value.F = 1f;
        Assert.Equal(new[] { 7f, 6f, 5f, 4f, 3f, 2f, 1f }, value.Values);
    }

    [Fact]
    public void TransferNamedValuesMatchNative()
    {
        Assert.Equal(new[] { 2.4f, 0.9478673f, 0.0521327f, 0.07739938f, 0.04045f, 0f, 0f }, SKColorSpaceTransferFn.Srgb.Values);
        Assert.Equal(new[] { 2.2f, 1f, 0f, 0f, 0f, 0f, 0f }, SKColorSpaceTransferFn.TwoDotTwo.Values);
        Assert.Equal(new[] { 1f, 1f, 0f, 0f, 0f, 0f, 0f }, SKColorSpaceTransferFn.Linear.Values);
        Assert.Equal(new[] { 2.22222f, 0.909672f, 0.0903276f, 0.222222f, 0.0812429f, 0f, 0f }, SKColorSpaceTransferFn.Rec2020.Values);
        Assert.Equal(new[] { -5f, 203f, 0f, 0f, 0f, 0f, 0f }, SKColorSpaceTransferFn.Pq.Values);
        Assert.Equal(new[] { -6f, 203f, 1000f, 1.2f, 0f, 0f, 0f }, SKColorSpaceTransferFn.Hlg.Values);
    }

    [Fact]
    public void TransferEvaluationMatchesNativeFastMath()
    {
        Assert.Equal(0.21399307f, SKColorSpaceTransferFn.Srgb.Transform(0.5f));
        Assert.Equal(-0.21399307f, SKColorSpaceTransferFn.Srgb.Transform(-0.5f));
        Assert.Equal(0.5000076f, SKColorSpaceTransferFn.Linear.Transform(0.5f));
        Assert.Equal(0.2596531f, SKColorSpaceTransferFn.Rec2020.Transform(0.5f));
        Assert.Equal(0f, SKColorSpaceTransferFn.Srgb.Transform(float.NaN));
        Assert.Equal(float.PositiveInfinity, SKColorSpaceTransferFn.Srgb.Transform(float.PositiveInfinity));
    }

    [Fact]
    public void TransferHdrEvaluationMatchesNative()
    {
        Assert.Equal(0.00922668f, SKColorSpaceTransferFn.Pq.Transform(0.5f));
        Assert.Equal(1f, SKColorSpaceTransferFn.Pq.Transform(-1f));
        Assert.Equal(0.083333336f, SKColorSpaceTransferFn.Hlg.Transform(0.5f));
        Assert.Equal(-0.083333336f, SKColorSpaceTransferFn.Hlg.Transform(-0.5f));
        Assert.Equal(0.02372241f, SKColorSpaceTransferFn.Hlg.Transform(float.NaN));
    }

    [Fact]
    public void TransferInversionMatchesNativeCoefficients()
    {
        Assert.Equal(
            new[] { 0.41666666f, 1.1372833f, -0f, 12.92f, 0.003130805f, -0.054969788f, -0f },
            SKColorSpaceTransferFn.Srgb.Invert().Values);
        Assert.Equal(new[] { 0.45454544f, 1f, -0f, 0f, 0f, 0f, 0f }, SKColorSpaceTransferFn.TwoDotTwo.Invert().Values);
        Assert.Equal(new[] { 1f, 1f, -0f, 0f, 0f, 0f, 0f }, SKColorSpaceTransferFn.Linear.Invert().Values);
        Assert.Equal(
            new[] { 0.45000046f, 1.2343903f, -0f, 4.5000043f, 0.018053958f, -0.09931946f, -0f },
            SKColorSpaceTransferFn.Rec2020.Invert().Values);
    }

    [Fact]
    public void TransferInvalidInversionReturnsEmpty()
    {
        Assert.Equal(SKColorSpaceTransferFn.Empty, SKColorSpaceTransferFn.Empty.Invert());
        Assert.Equal(SKColorSpaceTransferFn.Empty, SKColorSpaceTransferFn.Pq.Invert());
        Assert.Equal(SKColorSpaceTransferFn.Empty, SKColorSpaceTransferFn.Hlg.Invert());
        Assert.Equal(
            SKColorSpaceTransferFn.Empty,
            new SKColorSpaceTransferFn(2f, 3f, 4f, 5f, 6f, 7f, 8f).Invert());
    }

    [Fact]
    public void TransferValidationEqualityAndHashMatchNative()
    {
        Assert.Equal(
            "values",
            Assert.Throws<ArgumentNullException>(() => new SKColorSpaceTransferFn((float[])null!)).ParamName);
        Assert.Equal(
            "values",
            Assert.Throws<ArgumentException>(() => new SKColorSpaceTransferFn(new float[6])).ParamName);
        var value = SKColorSpaceTransferFn.Srgb;
        var equal = new SKColorSpaceTransferFn(value.Values);
        Assert.True(value == equal);
        Assert.False(value != equal);
        Assert.Equal(value.GetHashCode(), equal.GetHashCode());
        equal.F = 1f;
        Assert.NotEqual(value, equal);
    }
}
