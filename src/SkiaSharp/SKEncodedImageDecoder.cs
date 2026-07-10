using System;
using System.Buffers.Binary;

namespace SkiaSharp;

internal static class SKEncodedImageDecoder
{
    public static DecodedImage Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (IsIcon(data))
        {
            return DecodeIcon(data);
        }

        var result = StbImageSharp.ImageResult.FromMemory(
            data,
            StbImageSharp.ColorComponents.RedGreenBlueAlpha);
        return new DecodedImage(result.Width, result.Height, result.Data);
    }

    private static bool IsIcon(ReadOnlySpan<byte> data)
    {
        return data.Length >= 6
            && data[0] == 0
            && data[1] == 0
            && data[2] == 1
            && data[3] == 0;
    }

    private static DecodedImage DecodeIcon(byte[] data)
    {
        var span = data.AsSpan();
        var count = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(4, 2));
        if (count == 0 || span.Length < 6 + count * 16)
        {
            throw new InvalidOperationException("Invalid ICO directory.");
        }

        IconEntry? selected = null;
        for (var index = 0; index < count; index++)
        {
            var entryOffset = 6 + index * 16;
            var entry = span.Slice(entryOffset, 16);
            var width = entry[0] == 0 ? 256 : entry[0];
            var height = entry[1] == 0 ? 256 : entry[1];
            var bitCount = BinaryPrimitives.ReadUInt16LittleEndian(entry.Slice(6, 2));
            var byteCount = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(8, 4));
            var imageOffset = BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(12, 4));
            if ((ulong)imageOffset + byteCount > (ulong)span.Length || byteCount == 0)
            {
                continue;
            }

            var candidate = new IconEntry(width, height, bitCount, (int)imageOffset, (int)byteCount);
            if (selected == null
                || candidate.Width * candidate.Height > selected.Value.Width * selected.Value.Height
                || candidate.Width * candidate.Height == selected.Value.Width * selected.Value.Height
                    && candidate.BitCount > selected.Value.BitCount)
            {
                selected = candidate;
            }
        }

        if (selected == null)
        {
            throw new InvalidOperationException("ICO contains no valid image frames.");
        }

        var icon = selected.Value;
        var payload = span.Slice(icon.Offset, icon.ByteCount);
        if (payload.Length >= 8
            && payload[0] == 0x89
            && payload[1] == 0x50
            && payload[2] == 0x4e
            && payload[3] == 0x47)
        {
            var result = StbImageSharp.ImageResult.FromMemory(
                payload.ToArray(),
                StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            return new DecodedImage(result.Width, result.Height, result.Data);
        }

        return DecodeIconBitmap(payload, icon);
    }

    private static DecodedImage DecodeIconBitmap(ReadOnlySpan<byte> payload, IconEntry icon)
    {
        if (payload.Length < 40)
        {
            throw new InvalidOperationException("ICO bitmap header is truncated.");
        }

        var headerSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(0, 4)));
        var dibWidth = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(4, 4));
        var dibHeight = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(8, 4));
        var bitCount = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(14, 2));
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(16, 4));
        if (headerSize < 40 || headerSize > payload.Length || compression != 0 || bitCount is not (24 or 32))
        {
            throw new NotSupportedException("Only uncompressed 24-bit and 32-bit ICO bitmap frames are supported.");
        }

        var width = Math.Abs(dibWidth);
        var height = icon.Height > 0 ? icon.Height : Math.Abs(dibHeight) / 2;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("ICO bitmap dimensions are invalid.");
        }

        var bytesPerPixel = bitCount / 8;
        var xorRowBytes = checked(((width * bitCount + 31) / 32) * 4);
        var xorByteCount = checked(xorRowBytes * height);
        if (headerSize + xorByteCount > payload.Length)
        {
            throw new InvalidOperationException("ICO bitmap pixels are truncated.");
        }

        var rgba = new byte[checked(width * height * 4)];
        var bottomUp = dibHeight > 0;
        var hasAlpha = false;
        for (var y = 0; y < height; y++)
        {
            var sourceY = bottomUp ? height - 1 - y : y;
            var sourceRow = payload.Slice(headerSize + sourceY * xorRowBytes, xorRowBytes);
            for (var x = 0; x < width; x++)
            {
                var source = x * bytesPerPixel;
                var destination = (y * width + x) * 4;
                rgba[destination] = sourceRow[source + 2];
                rgba[destination + 1] = sourceRow[source + 1];
                rgba[destination + 2] = sourceRow[source];
                rgba[destination + 3] = bytesPerPixel == 4 ? sourceRow[source + 3] : (byte)255;
                hasAlpha |= rgba[destination + 3] != 0;
            }
        }

        if (!hasAlpha && bytesPerPixel == 4)
        {
            ApplyIconMask(payload, headerSize + xorByteCount, rgba, width, height, bottomUp);
        }

        return new DecodedImage(width, height, rgba);
    }

    private static void ApplyIconMask(
        ReadOnlySpan<byte> payload,
        int maskOffset,
        byte[] rgba,
        int width,
        int height,
        bool bottomUp)
    {
        var maskRowBytes = ((width + 31) / 32) * 4;
        if (maskOffset + maskRowBytes * height > payload.Length)
        {
            for (var pixel = 3; pixel < rgba.Length; pixel += 4)
            {
                rgba[pixel] = 255;
            }

            return;
        }

        for (var y = 0; y < height; y++)
        {
            var sourceY = bottomUp ? height - 1 - y : y;
            var maskRow = payload.Slice(maskOffset + sourceY * maskRowBytes, maskRowBytes);
            for (var x = 0; x < width; x++)
            {
                var transparent = (maskRow[x / 8] & (0x80 >> (x % 8))) != 0;
                rgba[(y * width + x) * 4 + 3] = transparent ? (byte)0 : (byte)255;
            }
        }
    }

    internal readonly record struct DecodedImage(int Width, int Height, byte[] Pixels);

    private readonly record struct IconEntry(int Width, int Height, ushort BitCount, int Offset, int ByteCount);
}
