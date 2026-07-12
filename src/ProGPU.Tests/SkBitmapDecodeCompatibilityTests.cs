using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkBitmapDecodeCompatibilityTests
{
    [Fact]
    public void DecodeEntryPointsUseNativePremultipliedDefaults()
    {
        var encoded = TwoPixelPngBytes();
        var path = Path.Combine(Path.GetTempPath(), $"progpu-decode-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, encoded);
        try
        {
            using var fromBytes = SKBitmap.Decode(encoded);
            using var fromSpan = SKBitmap.Decode(encoded.AsSpan());
            using var data = SKData.CreateCopy(encoded);
            using var fromData = SKBitmap.Decode(data);
            using var stream = new MemoryStream(encoded);
            using var fromStream = SKBitmap.Decode(stream);
            using var managedStream = new SKManagedStream(new MemoryStream(encoded));
            using var fromSkStream = SKBitmap.Decode(managedStream);
            using var codecData = SKData.CreateCopy(encoded);
            using var codec = SKCodec.Create(codecData);
            using var fromCodec = SKBitmap.Decode(codec);
            using var fromFile = SKBitmap.Decode(path);

            foreach (var bitmap in new[]
                     {
                         fromBytes,
                         fromSpan,
                         fromData,
                         fromStream,
                         fromSkStream,
                         fromCodec,
                         fromFile,
                     })
            {
                Assert.NotNull(bitmap);
                Assert.Equal(2, bitmap.Width);
                Assert.Equal(1, bitmap.Height);
                Assert.Equal(SKColorType.Rgba8888, bitmap.ColorType);
                Assert.Equal(SKAlphaType.Premul, bitmap.AlphaType);
                Assert.Equal(SKColors.Red, bitmap.GetPixel(0, 0));
                Assert.Equal(SKColors.Lime, bitmap.GetPixel(1, 0));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RequestedDecodeInfoControlsStorageAndScaling()
    {
        var info = new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var bitmap = SKBitmap.Decode(TwoPixelPngBytes(), info);

        Assert.NotNull(bitmap);
        Assert.Equal(info, bitmap.Info);
        Assert.Equal(4, bitmap.ByteCount);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(0, 0));
        Assert.Null(SKBitmap.Decode(TwoPixelPngBytes(), info.WithSize(0, 1)));
        Assert.Null(SKBitmap.Decode(TwoPixelPngBytes(), info.WithColorType(SKColorType.Alpha8)));
    }

    [Fact]
    public void BoundsAndInvalidInputContractsMatchNative()
    {
        var encoded = TwoPixelPngBytes();
        var invalid = new byte[] { 1, 2, 3, 4 };
        var path = Path.Combine(Path.GetTempPath(), $"progpu-bounds-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, encoded);
        try
        {
            using var data = SKData.CreateCopy(encoded);
            using var stream = new MemoryStream(encoded);
            using var managedStream = new SKManagedStream(new MemoryStream(encoded));
            var expected = new SKSizeI(2, 1);

            Assert.Equal(expected, SKBitmap.DecodeBounds(encoded).Size);
            Assert.Equal(expected, SKBitmap.DecodeBounds(encoded.AsSpan()).Size);
            Assert.Equal(expected, SKBitmap.DecodeBounds(data).Size);
            Assert.Equal(expected, SKBitmap.DecodeBounds(stream).Size);
            Assert.Equal(expected, SKBitmap.DecodeBounds(managedStream).Size);
            Assert.Equal(expected, SKBitmap.DecodeBounds(path).Size);
            Assert.True(SKBitmap.DecodeBounds(invalid).IsEmpty);
            Assert.Throws<ArgumentNullException>(() => SKBitmap.Decode(invalid));
            using var invalidData = SKData.CreateCopy(invalid);
            Assert.Null(SKBitmap.Decode(invalidData));
            Assert.Null(SKBitmap.Decode(path + ".missing"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] TwoPixelPngBytes() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAADklEQVR4nGP4z8DwHwQBEPgD/U6VwW8AAAAASUVORK5CYII=");
}
