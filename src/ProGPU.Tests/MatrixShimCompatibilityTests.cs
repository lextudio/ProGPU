using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class MatrixShimCompatibilityTests
{
    [Fact]
    public void SkMatrixConcatMatchesNativeSkiaOrder()
    {
        var scale = SKMatrix.CreateScale(2f, 3f);
        var translate = SKMatrix.CreateTranslation(10f, 20f);

        var result = SKMatrix.Concat(scale, translate);

        Assert.Equal(2f, result.ScaleX);
        Assert.Equal(3f, result.ScaleY);
        Assert.Equal(20f, result.TransX);
        Assert.Equal(60f, result.TransY);
    }
}
