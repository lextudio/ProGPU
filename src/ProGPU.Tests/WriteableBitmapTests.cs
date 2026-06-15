using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using ProGPU.Backend;
using Xunit;

namespace ProGPU.Tests;

public class WriteableBitmapTests
{
    [Fact]
    public void PixelFormatsAreDistinct()
    {
        Assert.NotEqual(PixelFormats.Bgr32, PixelFormats.Pbgra32);
        Assert.Equal("Bgr32", PixelFormats.Bgr32.ToString());
        Assert.Equal("Pbgra32", PixelFormats.Pbgra32.ToString());
    }

    [Fact]
    public void WritePixelsUploadBufferKeepsPbgraStrideForPaddedRows()
    {
        var upload = ConvertWritePixelsBuffer(
            new Int32Rect(0, 0, 2, 2),
            PixelFormats.Pbgra32,
            new byte[]
            {
                1, 2, 3, 4, 5, 6, 7, 8, 200, 201, 202, 203,
                9, 10, 11, 12, 13, 14, 15, 16, 204, 205, 206, 207
            },
            stride: 12);

        Assert.True(upload.IsValid);
        Assert.False(upload.IsCompact);
        Assert.Equal(12, upload.Stride);
        Assert.Equal(
            new byte[]
            {
                1, 2, 3, 4, 5, 6, 7, 8,
                9, 10, 11, 12, 13, 14, 15, 16
            },
            upload.CopyCompactRows());
    }

    [Fact]
    public void WritePixelsUploadBufferConvertsBgr32ToOpaquePbgra()
    {
        var upload = ConvertWritePixelsBuffer(
            new Int32Rect(0, 0, 2, 2),
            PixelFormats.Bgr32,
            new byte[]
            {
                10, 20, 30, 0, 40, 50, 60, 0, 200, 201, 202, 203,
                70, 80, 90, 0, 100, 110, 120, 0, 204, 205, 206, 207
            },
            stride: 12);

        Assert.True(upload.IsValid);
        Assert.True(upload.IsCompact);
        Assert.Equal(8, upload.Stride);
        Assert.Equal(
            new byte[]
            {
                10, 20, 30, 255,
                40, 50, 60, 255,
                70, 80, 90, 255,
                100, 110, 120, 255
            },
            upload.Pixels);
    }

    private static Pbgra32PixelBuffer ConvertWritePixelsBuffer(
        Int32Rect sourceRect,
        PixelFormat pixelFormat,
        byte[] pixels,
        int stride)
    {
        var method = typeof(WriteableBitmap).GetMethod(
            "ConvertWritePixelsBuffer",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        return (Pbgra32PixelBuffer)method.Invoke(null, new object[] { sourceRect, pixelFormat, pixels, stride })!;
    }
}
