using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkImageFilterFactoryCompatibilityTests
{
    [Fact]
    public void ImageFilterUsesSkObjectLifetime()
    {
        using var filter = SKImageFilter.CreateBlur(1f, 2f);
        Assert.IsAssignableFrom<SKObject>(filter);
        Assert.NotEqual(IntPtr.Zero, filter.Handle);

        filter.Dispose();

        Assert.Equal(IntPtr.Zero, filter.Handle);
    }

    [Fact]
    public void BlurOverloadsDefaultToDecalAndPreserveGraphState()
    {
        using var input = SKImageFilter.CreateOffset(2f, 3f);
        var crop = new SKRect(4f, 5f, 20f, 30f);
        using var defaultBlur = SKImageFilter.CreateBlur(1f, 2f);
        using var inputBlur = SKImageFilter.CreateBlur(1f, 2f, input, crop);
        using var tiledBlur = SKImageFilter.CreateBlur(
            3f,
            4f,
            SKShaderTileMode.Mirror,
            input,
            crop);

        Assert.Equal(SKShaderTileMode.Decal, Assert.IsType<SKImageFilter.BlurData>(defaultBlur.Parameters).TileMode);
        Assert.Same(input, inputBlur.Input);
        Assert.Equal(crop, inputBlur.CropRect);
        Assert.Equal(SKShaderTileMode.Mirror, Assert.IsType<SKImageFilter.BlurData>(tiledBlur.Parameters).TileMode);
    }

    [Fact]
    public void ArithmeticAndBlenderFactoriesRetainNullableSourceFallbacks()
    {
        var crop = new SKRect(1f, 2f, 30f, 40f);
        using var arithmetic = SKImageFilter.CreateArithmetic(
            .1f,
            .2f,
            .3f,
            .4f,
            true,
            background: null,
            foreground: null,
            crop);
        using var blender = SKBlender.CreateArithmetic(.5f, .6f, .7f, .8f, false);
        using var blended = SKImageFilter.CreateBlendMode(blender!, null, null, crop);

        var arithmeticData = Assert.IsType<SKImageFilter.ArithmeticData>(arithmetic.Parameters);
        Assert.Null(arithmeticData.Background);
        Assert.Null(arithmeticData.Foreground);
        Assert.Equal(crop, arithmetic.CropRect);

        var blendData = Assert.IsType<SKImageFilter.BlendModeData>(blended.Parameters);
        Assert.Same(blender, blendData.Blender);
        Assert.Null(blendData.Mode);
        Assert.Null(blendData.Background);
        Assert.Null(blendData.Foreground);
    }

    [Fact]
    public void MatrixConvolutionSpanRequiresExactKernelAndCopiesInput()
    {
        var kernel = new[] { 1f, 2f, 3f, 4f };
        using var filter = SKImageFilter.CreateMatrixConvolution(
            new SKSizeI(2, 2),
            kernel.AsSpan(),
            0.5f,
            0.25f,
            new SKPointI(1, 1),
            SKShaderTileMode.Clamp,
            true);

        kernel[0] = 99f;
        var data = Assert.IsType<SKImageFilter.MatrixConvolutionData>(filter.Parameters);
        Assert.Equal(new[] { 1f, 2f, 3f, 4f }, data.Kernel);

        var error = Assert.Throws<ArgumentException>(() =>
            SKImageFilter.CreateMatrixConvolution(
                new SKSizeI(2, 2),
                new[] { 1f, 2f, 3f }.AsSpan(),
                1f,
                0f,
                SKPointI.Empty,
                SKShaderTileMode.Decal,
                false));
        Assert.Equal("kernel", error.ParamName);
    }

    [Fact]
    public void MergeOverloadsCopyFiltersAndRetainNullSourceEntries()
    {
        using var first = SKImageFilter.CreateOffset(1f, 2f);
        using var second = SKImageFilter.CreateBlur(3f, 4f);
        var filters = new[] { first, second };
        using var spanMerge = SKImageFilter.CreateMerge(filters.AsSpan());
        using var pairMerge = SKImageFilter.CreateMerge(first: null, second);

        filters[0] = second;
        var copied = Assert.IsType<SKImageFilter?[]>(spanMerge.Parameters);
        Assert.Same(first, copied[0]);
        Assert.Same(second, copied[1]);

        var pair = Assert.IsType<SKImageFilter?[]>(pairMerge.Parameters);
        Assert.Null(pair[0]);
        Assert.Same(second, pair[1]);
    }

    [Fact]
    public void ShaderAndPictureFactoriesPreserveSourceGeneratingState()
    {
        using var emptyShader = SKImageFilter.CreateShader(shader: null);
        Assert.Null(Assert.IsType<SKImageFilter.ShaderData>(emptyShader.Parameters).Shader);

        using var recorder = new SKPictureRecorder();
        _ = recorder.BeginRecording(new SKRect(3f, 4f, 20f, 30f));
        using var picture = recorder.EndRecording();
        using var pictureFilter = SKImageFilter.CreatePicture(picture);
        var pictureData = Assert.IsType<SKImageFilter.PictureData>(pictureFilter.Parameters);
        Assert.Same(picture, pictureData.Picture);
        Assert.Equal(picture.CullRect, pictureData.TargetRect);
    }

    [Fact]
    public void FactoriesMatchNativeNullValidation()
    {
        Assert.Equal(
            "cf",
            Assert.Throws<ArgumentNullException>(
                () => SKImageFilter.CreateColorFilter(null!)).ParamName);
        Assert.Equal(
            "displacement",
            Assert.Throws<ArgumentNullException>(
                () => SKImageFilter.CreateDisplacementMapEffect(
                    SKColorChannel.R,
                    SKColorChannel.G,
                    1f,
                    null!)).ParamName);
        Assert.Equal(
            "image",
            Assert.Throws<ArgumentNullException>(
                () => SKImageFilter.CreateImage(null!)).ParamName);
        Assert.Equal(
            "picture",
            Assert.Throws<ArgumentNullException>(
                () => SKImageFilter.CreatePicture(null!)).ParamName);
        Assert.Equal(
            "blender",
            Assert.Throws<ArgumentNullException>(
                () => SKImageFilter.CreateBlendMode(null!, null)).ParamName);
        Assert.Equal(
            "input",
            Assert.Throws<ArgumentNullException>(
                () => SKImageFilter.CreateTile(SKRect.Empty, SKRect.Empty)).ParamName);
    }

    [Fact]
    public void ExistingGpuGraphFamiliesExposeExactCropOverloads()
    {
        var crop = new SKRect(1f, 2f, 30f, 40f);
        using var input = SKImageFilter.CreateOffset(1f, 2f);
        using var colorFilter = SKColorFilter.CreateBlendMode(SKColors.Red, SKBlendMode.Src);
        using var displacement = SKImageFilter.CreateShader(null);
        using var a = SKImageFilter.CreateColorFilter(colorFilter, input, crop);
        using var b = SKImageFilter.CreateDilate(1f, 2f, input, crop);
        using var c = SKImageFilter.CreateErode(1f, 2f, input, crop);
        using var d = SKImageFilter.CreateDisplacementMapEffect(
            SKColorChannel.R,
            SKColorChannel.G,
            3f,
            displacement,
            input,
            crop);
        using var e = SKImageFilter.CreateDropShadow(1f, 2f, 3f, 4f, SKColors.Black, input, crop);
        using var f = SKImageFilter.CreateDropShadowOnly(1f, 2f, 3f, 4f, SKColors.Black, input, crop);
        using var g = SKImageFilter.CreateDistantLitDiffuse(new SKPoint3(1f, 2f, 3f), SKColors.White, 1f, 2f, input, crop);
        using var h = SKImageFilter.CreateDistantLitSpecular(new SKPoint3(1f, 2f, 3f), SKColors.White, 1f, 2f, 3f, input, crop);
        using var i = SKImageFilter.CreatePointLitDiffuse(new SKPoint3(1f, 2f, 3f), SKColors.White, 1f, 2f, input, crop);
        using var j = SKImageFilter.CreatePointLitSpecular(new SKPoint3(1f, 2f, 3f), SKColors.White, 1f, 2f, 3f, input, crop);
        using var k = SKImageFilter.CreateSpotLitDiffuse(new SKPoint3(1f, 2f, 3f), new SKPoint3(4f, 5f, 6f), 1f, 45f, SKColors.White, 2f, 3f, input, crop);
        using var l = SKImageFilter.CreateSpotLitSpecular(new SKPoint3(1f, 2f, 3f), new SKPoint3(4f, 5f, 6f), 1f, 45f, SKColors.White, 2f, 3f, 4f, input, crop);
        using var m = SKImageFilter.CreateOffset(1f, 2f, input, crop);

        foreach (var filter in new[] { a, b, c, d, e, f, g, h, i, j, k, l, m })
        {
            Assert.Equal(crop, filter.CropRect);
        }
    }
}
