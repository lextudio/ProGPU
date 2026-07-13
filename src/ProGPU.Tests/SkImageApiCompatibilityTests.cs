using System.Reflection;
using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkImageApiCompatibilityTests
{
    [Fact]
    public void ImageUsesSkiaObjectLifetimeAndHidesBackendTexture()
    {
        Assert.Equal(typeof(SKObject), typeof(SKImage).BaseType);
        Assert.Null(typeof(SKImage).GetProperty(
            "Texture",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Null(typeof(SKImage).GetMethod(
            "Dispose",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void ImageExposesSamplingAwareShaderAndPixelContracts()
    {
        AssertMethod(
            nameof(SKImage.ToShader),
            typeof(SKShader),
            typeof(SKShaderTileMode),
            typeof(SKShaderTileMode),
            typeof(SKSamplingOptions),
            typeof(SKMatrix));
        AssertMethod(
            nameof(SKImage.ToRawShader),
            typeof(SKShader),
            typeof(SKShaderTileMode),
            typeof(SKShaderTileMode),
            typeof(SKSamplingOptions),
            typeof(SKMatrix));
        AssertMethod(
            nameof(SKImage.ReadPixels),
            typeof(bool),
            typeof(SKImageInfo),
            typeof(IntPtr),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(SKImageCachingHint));
        AssertMethod(
            nameof(SKImage.ScalePixels),
            typeof(bool),
            typeof(SKPixmap),
            typeof(SKSamplingOptions),
            typeof(SKImageCachingHint));
    }

    private static void AssertMethod(string name, Type returnType, params Type[] parameterTypes)
    {
        var method = typeof(SKImage).GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            binder: null,
            parameterTypes,
            modifiers: null);
        Assert.NotNull(method);
        Assert.Equal(returnType, method.ReturnType);
    }
}
