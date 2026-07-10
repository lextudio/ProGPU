using System;
using System.IO;
using System.Numerics;
using ProGPU.Backend;

namespace SkiaSharp;

public delegate void SKBitmapReleaseDelegate(IntPtr address, object context);

public enum SKColorType
{
    Unknown = 0,
    Alpha8 = 1,
    Rgb565 = 2,
    Argb4444 = 3,
    Rgba8888 = 4,
    Rgb888x = 5,
    Bgra8888 = 6,
    RgbaF16 = 7,
    RgbaF32 = 8,
}

public enum SKAlphaType
{
    Unknown = 0,
    Opaque = 1,
    Premul = 2,
    Unpremul = 3,
}

public enum SKBlendMode
{
    Clear = 0,
    Src = 1,
    Dst = 2,
    SrcOver = 3,
    DstOver = 4,
    SrcIn = 5,
    DstIn = 6,
    SrcOut = 7,
    DstOut = 8,
    SrcATop = 9,
    DstATop = 10,
    Xor = 11,
    Plus = 12,
    Modulate = 13,
    Screen = 14,
    Overlay = 15,
    Darken = 16,
    Lighten = 17,
    ColorDodge = 18,
    ColorBurn = 19,
    HardLight = 20,
    SoftLight = 21,
    Difference = 22,
    Exclusion = 23,
    Multiply = 24,
    Hue = 25,
    Saturation = 26,
    Color = 27,
    Luminosity = 28,
}

public enum SKClipOperation
{
    Difference = 0,
    Intersect = 1,
}

public enum SKFilterMode
{
    Nearest = 0,
    Linear = 1,
}

public enum SKMipmapMode
{
    None = 0,
    Nearest = 1,
    Linear = 2,
}

public enum SKShaderTileMode
{
    Clamp = 0,
    Repeat = 1,
    Mirror = 2,
    Decal = 3,
}

public enum SKTextAlign
{
    Left = 0,
    Center = 1,
    Right = 2,
}

public enum SKStrokeCap
{
    Butt = 0,
    Round = 1,
    Square = 2,
}

public enum SKStrokeJoin
{
    Miter = 0,
    Round = 1,
    Bevel = 2,
}

public enum SKFontStyleSlant
{
    Upright = 0,
    Italic = 1,
    Oblique = 2,
}

public enum SKFontHinting
{
    None = 0,
    Slight = 1,
    Normal = 2,
    Full = 3,
}

public enum SKFontEdging
{
    Alias = 0,
    Antialias = 1,
    SubpixelAntialias = 2,
}

public enum SKPathOp
{
    Difference = 0,
    Intersect = 1,
    Union = 2,
    Xor = 3,
    ReverseDifference = 4,
}

public enum SKPathFillType
{
    Winding = 0,
    EvenOdd = 1,
    InverseWinding = 2,
    InverseEvenOdd = 3,
}

public enum SKPathArcSize
{
    Small = 0,
    Large = 1,
}

public enum SKPathDirection
{
    Clockwise = 0,
    CounterClockwise = 1,
}

public enum SKPixelGeometry
{
    Unknown = 0,
    UnknownHorizontal = 1,
    UnknownVertical = 2,
    RgbHorizontal = 3,
    RgbVertical = 4,
    BgrHorizontal = 5,
    BgrVertical = 6,
}

public enum SKEncodedImageFormat
{
    Bmp = 0,
    Gif = 1,
    Ico = 2,
    Jpeg = 3,
    Png = 4,
    Wbmp = 5,
    Webp = 6,
    Pkm = 7,
    Ktx = 8,
    Astc = 9,
    Dng = 10,
    Heif = 11,
}

public enum SKImageCachingHint
{
    Allow = 0,
    Disallow = 1,
}

public enum SKRegionOperation
{
    Difference = 0,
    Intersect = 1,
    Union = 2,
    Xor = 3,
    ReverseDifference = 4,
    Replace = 5,
}

public struct SKPoint
{
    public float X;
    public float Y;

    public SKPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static readonly SKPoint Empty = new(0, 0);
    public override string ToString() => $"({X}, {Y})";
}

public struct SKSize
{
    public float Width;
    public float Height;

    public SKSize(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public static readonly SKSize Empty = new(0, 0);
}

public struct SKSizeI
{
    public int Width;
    public int Height;

    public SKSizeI(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public static readonly SKSizeI Empty = new(0, 0);
}

public struct SKRect
{
    public float Left;
    public float Top;
    public float Right;
    public float Bottom;

    public float Width => Right - Left;
    public float Height => Bottom - Top;
    public float MidX => Left + Width / 2f;
    public float MidY => Top + Height / 2f;
    public bool IsEmpty => Left >= Right || Top >= Bottom;

    public SKRect(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public static readonly SKRect Empty = new(0, 0, 0, 0);

    public void Union(SKRect rect)
    {
        if (rect.IsEmpty)
        {
            return;
        }

        if (IsEmpty)
        {
            this = rect;
            return;
        }

        Left = Math.Min(Left, rect.Left);
        Top = Math.Min(Top, rect.Top);
        Right = Math.Max(Right, rect.Right);
        Bottom = Math.Max(Bottom, rect.Bottom);
    }

    public void Inflate(float amount)
    {
        Inflate(amount, amount);
    }

    public void Inflate(float x, float y)
    {
        Left -= x;
        Top -= y;
        Right += x;
        Bottom += y;
    }

    public void Offset(float x, float y)
    {
        Left += x;
        Top += y;
        Right += x;
        Bottom += y;
    }
}

public struct SKRectI
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public SKRectI(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public static readonly SKRectI Empty = new(0, 0, 0, 0);
}

public struct SKColor
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }
    public byte Red => R;
    public byte Green => G;
    public byte Blue => B;
    public byte Alpha => A;

    public SKColor(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public SKColor(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
        A = 255;
    }

    public static readonly SKColor Empty = new(0, 0, 0, 0);

    public static implicit operator SKColor(uint val)
    {
        byte a = (byte)((val >> 24) & 0xFF);
        byte r = (byte)((val >> 16) & 0xFF);
        byte g = (byte)((val >> 8) & 0xFF);
        byte b = (byte)(val & 0xFF);
        return new SKColor(r, g, b, a);
    }

    public static implicit operator uint(SKColor color)
    {
        return ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
    }
}

public static class SKColors
{
    public static readonly SKColor Black = new(0, 0, 0, 255);
    public static readonly SKColor White = new(255, 255, 255, 255);
    public static readonly SKColor Red = new(255, 0, 0, 255);
    public static readonly SKColor Green = new(0, 255, 0, 255);
    public static readonly SKColor Blue = new(0, 0, 255, 255);
    public static readonly SKColor Transparent = new(0, 0, 0, 0);
}

public struct SKColorF
{
    public float R;
    public float G;
    public float B;
    public float A;

    public SKColorF(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }
}

public struct SKMatrix
{
    public float ScaleX;
    public float SkewX;
    public float TransX;
    public float SkewY;
    public float ScaleY;
    public float TransY;
    public float Persp0;
    public float Persp1;
    public float Persp2;

    public static readonly SKMatrix Identity = new()
    {
        ScaleX = 1f, ScaleY = 1f, Persp2 = 1f
    };

    public Matrix4x4 ToMatrix4x4()
    {
        return new Matrix4x4(
            ScaleX, SkewY, 0f, 0f,
            SkewX, ScaleY, 0f, 0f,
            0f, 0f, 1f, 0f,
            TransX, TransY, 0f, 1f
        );
    }

    public static SKMatrix CreateIdentity() => Identity;

    public static SKMatrix CreateTranslation(float x, float y)
    {
        var matrix = Identity;
        matrix.TransX = x;
        matrix.TransY = y;
        return matrix;
    }

    public static SKMatrix CreateScale(float x, float y)
    {
        var matrix = Identity;
        matrix.ScaleX = x;
        matrix.ScaleY = y;
        return matrix;
    }

    public static SKMatrix CreateScale(float x, float y, float pivotX, float pivotY)
    {
        return FromMatrix4x4(
            Matrix4x4.CreateTranslation(-pivotX, -pivotY, 0f)
            * Matrix4x4.CreateScale(x, y, 1f)
            * Matrix4x4.CreateTranslation(pivotX, pivotY, 0f));
    }

    public static SKMatrix CreateRotationDegrees(float degrees)
    {
        return CreateRotationDegrees(degrees, 0f, 0f);
    }

    public static SKMatrix CreateRotationDegrees(float degrees, float pivotX, float pivotY)
    {
        var radians = degrees * MathF.PI / 180f;
        return FromMatrix4x4(
            Matrix4x4.CreateTranslation(-pivotX, -pivotY, 0f)
            * Matrix4x4.CreateRotationZ(radians)
            * Matrix4x4.CreateTranslation(pivotX, pivotY, 0f));
    }

    public SKMatrix PreConcat(SKMatrix matrix)
    {
        return FromMatrix4x4(matrix.ToMatrix4x4() * ToMatrix4x4());
    }

    public SKMatrix PostConcat(SKMatrix matrix)
    {
        return FromMatrix4x4(ToMatrix4x4() * matrix.ToMatrix4x4());
    }

    public static SKMatrix Concat(SKMatrix first, SKMatrix second)
    {
        return FromMatrix4x4(second.ToMatrix4x4() * first.ToMatrix4x4());
    }

    public static void Concat(ref SKMatrix result, SKMatrix first, SKMatrix second)
    {
        result = Concat(first, second);
    }

    internal static SKMatrix FromMatrix4x4(Matrix4x4 matrix)
    {
        return new SKMatrix
        {
            ScaleX = matrix.M11,
            SkewX = matrix.M21,
            TransX = matrix.M41,
            SkewY = matrix.M12,
            ScaleY = matrix.M22,
            TransY = matrix.M42,
            Persp0 = matrix.M14,
            Persp1 = matrix.M24,
            Persp2 = matrix.M44
        };
    }
}

public class SKMatrix44
{
    public float M00 { get; set; } = 1f;
    public float M01 { get; set; }
    public float M02 { get; set; }
    public float M03 { get; set; }
    public float M10 { get; set; }
    public float M11 { get; set; } = 1f;
    public float M12 { get; set; }
    public float M13 { get; set; }
    public float M20 { get; set; }
    public float M21 { get; set; }
    public float M22 { get; set; } = 1f;
    public float M23 { get; set; }
    public float M30 { get; set; }
    public float M31 { get; set; }
    public float M32 { get; set; }
    public float M33 { get; set; } = 1f;

    public Matrix4x4 ToMatrix4x4()
    {
        return new Matrix4x4(
            M00, M01, M02, M03,
            M10, M11, M12, M13,
            M20, M21, M22, M23,
            M30, M31, M32, M33);
    }

    internal static SKMatrix44 FromMatrix4x4(Matrix4x4 matrix)
    {
        return new SKMatrix44
        {
            M00 = matrix.M11,
            M01 = matrix.M12,
            M02 = matrix.M13,
            M03 = matrix.M14,
            M10 = matrix.M21,
            M11 = matrix.M22,
            M12 = matrix.M23,
            M13 = matrix.M24,
            M20 = matrix.M31,
            M21 = matrix.M32,
            M22 = matrix.M33,
            M23 = matrix.M34,
            M30 = matrix.M41,
            M31 = matrix.M42,
            M32 = matrix.M43,
            M33 = matrix.M44
        };
    }
}

public struct SKCubicResampler
{
    public float B;
    public float C;

    public SKCubicResampler(float b, float c)
    {
        B = b;
        C = c;
    }

    public static readonly SKCubicResampler Mitchell = new(1f / 3f, 1f / 3f);
    public static readonly SKCubicResampler CatmullRom = new(0f, 0.5f);
}

public struct SKSamplingOptions
{
    public SKFilterMode FilterMode;
    public SKMipmapMode MipmapMode;
    public bool UseCubic;
    public SKCubicResampler CubicResampler;

    public SKSamplingOptions(SKFilterMode filterMode, SKMipmapMode mipmapMode)
    {
        FilterMode = filterMode;
        MipmapMode = mipmapMode;
        UseCubic = false;
        CubicResampler = default;
    }

    public SKSamplingOptions(SKCubicResampler cubicResampler)
    {
        FilterMode = default;
        MipmapMode = default;
        UseCubic = true;
        CubicResampler = cubicResampler;
    }
}

public class SKColorSpace : IDisposable
{
    public static SKColorSpace CreateSrgb() => new();
    public void Dispose() { }
}

public struct SKImageInfo
{
    public int Width;
    public int Height;
    public SKColorType ColorType;
    public SKAlphaType AlphaType;
    public SKColorSpace? ColorSpace;

    public int BytesPerPixel => ColorType switch
    {
        SKColorType.Alpha8 => 1,
        SKColorType.Rgb565 or SKColorType.Argb4444 => 2,
        SKColorType.RgbaF16 => 8,
        SKColorType.RgbaF32 => 16,
        _ => 4
    };
    public int RowBytes => checked(Width * BytesPerPixel);
    public int BytesSize => RowBytes * Height;

    public SKImageInfo(int width, int height, SKColorType colorType = SKColorType.Rgba8888, SKAlphaType alphaType = SKAlphaType.Premul, SKColorSpace? colorSpace = null)
    {
        Width = width;
        Height = height;
        ColorType = colorType;
        AlphaType = alphaType;
        ColorSpace = colorSpace;
    }

    public static readonly SKColorType PlatformColorType = SKColorType.Rgba8888;
}

public abstract class SKStream : IDisposable
{
    public virtual void Dispose() { }
}

public class SKData : IDisposable
{
    public byte[] Bytes { get; }

    public SKData(byte[] bytes)
    {
        Bytes = bytes;
    }

    public static SKData CreateCopy(IntPtr address, int length)
    {
        byte[] buffer = new byte[length];
        System.Runtime.InteropServices.Marshal.Copy(address, buffer, 0, length);
        return new SKData(buffer);
    }

    public static SKData Create(SKStream stream)
    {
        if (stream is SKManagedStream managed)
        {
            using (var ms = new MemoryStream())
            {
                managed.Stream.CopyTo(ms);
                return new SKData(ms.ToArray());
            }
        }
        return new SKData(Array.Empty<byte>());
    }

    public static SKData Create(Stream stream)
    {
        using (var ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            return new SKData(ms.ToArray());
        }
    }

    public void SaveTo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(Bytes, 0, Bytes.Length);
    }

    public void Dispose() { }
}

public class SKCodec : IDisposable
{
    private readonly byte[] _data;
    internal byte[] EncodedBytes => _data;

    private SKCodec(byte[] data)
    {
        _data = data;
        var decoded = SKEncodedImageDecoder.Decode(data);
        Info = new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
    }

    public SKImageInfo Info { get; }

    public SKSizeI GetScaledDimensions(float desiredScale)
    {
        if (!float.IsFinite(desiredScale) || desiredScale <= 0f)
        {
            return new SKSizeI(Info.Width, Info.Height);
        }

        return new SKSizeI(
            Math.Max(1, (int)MathF.Round(Info.Width * MathF.Min(desiredScale, 1f))),
            Math.Max(1, (int)MathF.Round(Info.Height * MathF.Min(desiredScale, 1f))));
    }

    public static SKCodec Create(SKData data)
    {
        return new SKCodec(data.Bytes);
    }

    public static SKCodec Create(SKStream stream)
    {
        using (var data = SKData.Create(stream))
        {
            return new SKCodec(data.Bytes);
        }
    }

    public static SKCodec Create(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return new SKCodec(ms.ToArray());
    }

    public void Dispose() { }
}

public class SKDocument : IDisposable
{
    public void Dispose() { }
}

public class SKSurfaceProperties : IDisposable
{
    public SKPixelGeometry PixelGeometry { get; }

    public SKSurfaceProperties(SKPixelGeometry pixelGeometry)
    {
        PixelGeometry = pixelGeometry;
    }

    public void Dispose()
    {
    }
}

internal static class SKContextHelper
{
    private static WgpuContext? _fallbackContext;
    public static WgpuContext GetContext()
    {
        if (WgpuContext.Current != null && !WgpuContext.Current.IsDisposed)
            return WgpuContext.Current;

        if (WgpuContext.TryGetFirstActiveContext(out var ctx))
        {
            return ctx;
        }

        if (_fallbackContext == null || _fallbackContext.IsDisposed)
        {
            _fallbackContext = new WgpuContext();
            _fallbackContext.Initialize(null);
        }
        return _fallbackContext;
    }
}
