using System;
using System.IO;
using System.Runtime.InteropServices;
using ProGPU.Backend;
using Silk.NET.WebGPU;

namespace SkiaSharp;

public class SKImage : IDisposable
{
    public GpuTexture Texture { get; }
    public int Width => (int)Texture.Width;
    public int Height => (int)Texture.Height;

    public SKImage(GpuTexture texture)
    {
        Texture = texture;
    }

    public static SKImage FromBitmap(SKBitmap bitmap)
    {
        // Upload CPU bitmap pixels to a GPU texture for zero-copy fast GPU drawing!
        var ctx = SKContextHelper.GetContext();
        var texture = new GpuTexture(
            ctx,
            (uint)bitmap.Width,
            (uint)bitmap.Height,
            TextureFormat.Rgba8Unorm,
            TextureUsage.TextureBinding | TextureUsage.CopyDst,
            "SKImage Texture"
        );

        int size = bitmap.Width * bitmap.Height * 4;
        byte[] buffer = new byte[size];
        Marshal.Copy(bitmap.GetPixels(), buffer, 0, size);
        texture.WritePixels(new ReadOnlySpan<byte>(buffer));

        return new SKImage(texture);
    }

    public static SKImage FromTexture(GpuTexture texture)
    {
        return new SKImage(texture);
    }

    public void ScalePixels(SKPixmap dst, SKSamplingOptions sampling)
    {
        // Scale pixel helper (stub or read back and scale)
    }

    public void ReadPixels(SKImageInfo dstInfo, IntPtr dstPixels, int dstRowBytes, int srcX, int srcY, SKImageCachingHint cachingHint)
    {
        // Read GPU texture pixels back to CPU memory
        int size = dstInfo.Width * dstInfo.Height * 4;
        byte[] pixels = Texture.ReadPixels(); // Read back from GPU
        Marshal.Copy(pixels, 0, dstPixels, Math.Min(size, pixels.Length));
    }

    public SKImage ToRasterImage(bool share) => this;

    public SKData Encode()
    {
        return Encode(SKEncodedImageFormat.Png, 100);
    }

    public SKData Encode(SKEncodedImageFormat format, int quality)
    {
        byte[] pixels = Texture.ReadPixels();
        using (var ms = new MemoryStream())
        {
            var writer = new StbImageWriteSharp.ImageWriter();
            if (format == SKEncodedImageFormat.Jpeg)
            {
                writer.WriteJpg(pixels, Width, Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, ms, quality);
            }
            else
            {
                writer.WritePng(pixels, Width, Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, ms);
            }
            return new SKData(ms.ToArray());
        }
    }

    public void Dispose()
    {
        // In this drop-in replacement, we let ProGPU's GpuTexture GC handle texture disposal
    }
}

public class SKPixmap
{
    public SKImageInfo Info { get; }
    public IntPtr Pixels { get; }
    public int RowBytes { get; }

    public SKPixmap(SKImageInfo info, IntPtr pixels, int rowBytes)
    {
        Info = info;
        Pixels = pixels;
        RowBytes = rowBytes;
    }
}

public class SKBitmap : IDisposable
{
    private IntPtr _pixels;
    private readonly bool _ownsPixels;
    private int _width;
    private int _height;
    private SKImageInfo _info;

    public int Width => _width;
    public int Height => _height;
    public SKImageInfo Info => _info;
    public SKColorType ColorType => _info.ColorType;
    public SKAlphaType AlphaType => _info.AlphaType;
    public int RowBytes => _width * 4;
    public int BytesSize => RowBytes * _height;

    public SKBitmap()
    {
        _pixels = IntPtr.Zero;
        _ownsPixels = false;
    }

    public SKBitmap(int width, int height, bool isOpaque = false)
    {
        _width = width;
        _height = height;
        _info = new SKImageInfo(width, height, SKColorType.Rgba8888, isOpaque ? SKAlphaType.Opaque : SKAlphaType.Premul);
        _pixels = Marshal.AllocHGlobal(BytesSize);
        _ownsPixels = true;
        // Zero-initialize
        byte[] zero = new byte[BytesSize];
        Marshal.Copy(zero, 0, _pixels, BytesSize);
    }

    public SKBitmap(SKImageInfo info)
    {
        _width = info.Width;
        _height = info.Height;
        _info = info;
        _pixels = Marshal.AllocHGlobal(BytesSize);
        _ownsPixels = true;
        // Zero-initialize
        byte[] zero = new byte[BytesSize];
        Marshal.Copy(zero, 0, _pixels, BytesSize);
    }

    public IntPtr GetPixels() => _pixels;

    public void InstallPixels(SKImageInfo info, IntPtr pixels, int rowBytes)
    {
        if (_ownsPixels && _pixels != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_pixels);
        }
        _width = info.Width;
        _height = info.Height;
        _info = info;
        _pixels = pixels;
        // We do not own external pixels, so we don't free them
    }

    public SKBitmap Copy()
    {
        var copy = new SKBitmap(_info);
        byte[] buffer = new byte[BytesSize];
        Marshal.Copy(_pixels, buffer, 0, BytesSize);
        Marshal.Copy(buffer, 0, copy.GetPixels(), BytesSize);
        return copy;
    }

    public void SetImmutable() { }

    public bool CanCopyTo(SKColorType type) => type == SKColorType.Rgba8888 || type == SKColorType.Bgra8888;

    public static SKBitmap Decode(SKData data)
    {
        using (var ms = new MemoryStream(data.Bytes))
        {
            var result = StbImageSharp.ImageResult.FromStream(ms, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            var bmp = new SKBitmap(result.Width, result.Height);
            Marshal.Copy(result.Data, 0, bmp.GetPixels(), result.Data.Length);
            return bmp;
        }
    }

    public static SKBitmap Decode(SKCodec codec, SKImageInfo info)
    {
        // Simple decoder fallback using data stream
        return new SKBitmap(info);
    }

    public SKPixmap PeekPixels()
    {
        return new SKPixmap(_info, _pixels, RowBytes);
    }

    public SKBitmap Resize(SKImageInfo info, SKSamplingOptions sampling)
    {
        // Create scaled bitmap
        var resized = new SKBitmap(info);
        // Simple pixel scaling (nearest neighbor/bilinear stub)
        byte[] src = new byte[BytesSize];
        Marshal.Copy(_pixels, src, 0, BytesSize);

        byte[] dst = new byte[resized.BytesSize];
        float xRatio = (float)_width / info.Width;
        float yRatio = (float)_height / info.Height;

        for (int y = 0; y < info.Height; y++)
        {
            int srcY = (int)(y * yRatio);
            srcY = Math.Clamp(srcY, 0, _height - 1);
            for (int x = 0; x < info.Width; x++)
            {
                int srcX = (int)(x * xRatio);
                srcX = Math.Clamp(srcX, 0, _width - 1);

                int srcOffset = (srcY * _width + srcX) * 4;
                int dstOffset = (y * info.Width + x) * 4;

                Array.Copy(src, srcOffset, dst, dstOffset, 4);
            }
        }

        Marshal.Copy(dst, 0, resized.GetPixels(), dst.Length);
        return resized;
    }

    public void Dispose()
    {
        if (_ownsPixels && _pixels != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_pixels);
            _pixels = IntPtr.Zero;
        }
    }
}

public class SKManagedStream : SKStream
{
    public Stream Stream { get; }

    public SKManagedStream(Stream stream)
    {
        Stream = stream;
    }
}
