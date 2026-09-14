using System.Buffers.Binary;
using StbImageSharp;

namespace ProGPU.GameEngine.Assets;

/// <summary>
/// Owned straight-alpha RGBA8 pixels, decoded on asset preparation rather than rendering.
/// PNG/GIF/BMP dimensions are checked before the existing reviewed decoder dependency
/// allocates pixels. Decode costs O(E + P) for encoded bytes E and pixels P; owned
/// storage is 4P bytes, in addition to dependency-owned decoding scratch. GIF decoding selects its first
/// image, not a time-based animated GIF playback contract.
/// </summary>
public sealed class RasterImage
{
    public const int MaximumDimension = 8192;
    public const int MaximumPixels = 4 * 1024 * 1024;
    private readonly byte[] _pixels;
    public int Width { get; }
    public int Height { get; }
    public int ByteLength => _pixels.Length;
    public ReadOnlySpan<byte> Pixels => _pixels;
    private RasterImage(int width, int height, byte[] pixels) { Width = width; Height = height; _pixels = pixels; }

    public static (int Width, int Height) ReadDimensions(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > AssetBundle.MaximumFileBytes) throw new FormatException("Encoded images must be at most 8 MiB.");
        long width, height;
        if (bytes.Length >= 33 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            if (BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]) != 13) throw new FormatException("Malformed PNG header.");
            width = BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]); height = BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]);
        }
        else if (bytes.Length >= 13 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8)))
        { width = BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]); height = BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]); }
        else if (bytes.Length >= 26 && bytes[..2].SequenceEqual("BM"u8))
        {
            uint dib = BinaryPrimitives.ReadUInt32LittleEndian(bytes[14..]);
            if (dib == 12) { width = BinaryPrimitives.ReadUInt16LittleEndian(bytes[18..]); height = BinaryPrimitives.ReadUInt16LittleEndian(bytes[20..]); }
            else if (dib >= 40 && bytes.Length >= 54)
            { width = BinaryPrimitives.ReadInt32LittleEndian(bytes[18..]); height = Math.Abs((long)BinaryPrimitives.ReadInt32LittleEndian(bytes[22..])); }
            else throw new FormatException("Unsupported BMP header.");
        }
        else throw new FormatException("Expected a PNG, GIF or BMP image.");
        if (width <= 0 || height <= 0 || width > MaximumDimension || height > MaximumDimension || width * height > MaximumPixels)
            throw new FormatException("Images must fit 8192 per axis and four million pixels.");
        return ((int)width, (int)height);
    }

    public static RasterImage DecodeFirstFrame(ReadOnlySpan<byte> bytes)
    {
        var size = ReadDimensions(bytes);
        ImageResult result;
        try { result = ImageResult.FromMemory(bytes.ToArray(), ColorComponents.RedGreenBlueAlpha); }
        catch (Exception error) when (error is not OutOfMemoryException)
        { throw new FormatException("The image decoder could not read this asset.", error); }
        if (result.Width != size.Width || result.Height != size.Height || result.Data.Length != checked(size.Width * size.Height * 4))
            throw new FormatException("Decoded image dimensions differ from its bounded header.");
        return new(size.Width, size.Height, result.Data);
    }

    public static RasterImage FromRgba(int width, int height, ReadOnlySpan<byte> pixels)
    {
        if (width <= 0 || height <= 0 || width > MaximumDimension || height > MaximumDimension || (long)width * height > MaximumPixels ||
            (long)width * height * 4 != pixels.Length) throw new ArgumentOutOfRangeException(nameof(width));
        return new(width, height, pixels.ToArray());
    }

    /// <summary>
    /// Black RGB mask pixels keep opaque color, white pixels clear it. Rejects
    /// colored/gray masks instead of approximating channel-wise raster operations.
    /// Optional opaque-source mode ignores an encoded GIF alpha channel. O(P)
    /// time and one 4P-byte output; both inputs remain unchanged.
    /// </summary>
    public RasterImage WithBinaryMask(RasterImage mask, bool ignoreSourceAlpha = false, bool requireBlackOutsideMask = false)
    {
        ArgumentNullException.ThrowIfNull(mask);
        if (Width != mask.Width || Height != mask.Height) throw new FormatException("Image and mask dimensions must match.");
        var pixels = (byte[])_pixels.Clone();
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte value = mask._pixels[i];
            if (value is not (0 or 255) || mask._pixels[i + 1] != value || mask._pixels[i + 2] != value)
                throw new FormatException("Only black/white opacity masks are supported; colored and gray raster-operation masks need separate rendering.");
            if (requireBlackOutsideMask && value == 255 && (pixels[i] != 0 || pixels[i + 1] != 0 || pixels[i + 2] != 0))
                throw new FormatException("A legacy raster-operation mask requires black color pixels outside the mask.");
            pixels[i + 3] = value == 255 ? (byte)0 : ignoreSourceAlpha ? (byte)255 : pixels[i + 3];
        }
        return new(Width, Height, pixels);
    }
}
