using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace ProGPU.Text;

internal static class SfntFontContainer
{
    private const uint Woff1Signature = 0x774F4646;
    private const uint Woff2Signature = 0x774F4632;
    private const int WoffHeaderSize = 44;
    private const int WoffDirectoryEntrySize = 20;
    private const int SfntHeaderSize = 12;
    private const int SfntDirectoryEntrySize = 16;

    public static byte[] Normalize(byte[] fontData)
    {
        ArgumentNullException.ThrowIfNull(fontData);
        if (fontData.Length < 4)
        {
            return fontData;
        }

        return ReadUInt(fontData, 0) switch
        {
            Woff1Signature => DecodeWoff1(fontData),
            Woff2Signature => throw new FormatException("WOFF2 font containers are not supported."),
            _ => fontData
        };
    }

    private static byte[] DecodeWoff1(byte[] data)
    {
        if (data.Length < WoffHeaderSize)
        {
            throw new FormatException("WOFF header is truncated.");
        }

        var flavor = ReadUInt(data, 4);
        var declaredLength = ReadUInt(data, 8);
        var tableCount = ReadUShort(data, 12);
        var reserved = ReadUShort(data, 14);
        var declaredSfntSize = ReadUInt(data, 16);
        if (declaredLength < WoffHeaderSize || declaredLength > data.Length)
        {
            throw new FormatException("WOFF container length is invalid.");
        }

        if (tableCount == 0 || reserved != 0)
        {
            throw new FormatException("WOFF header contains invalid table metadata.");
        }

        var directoryLength = checked(tableCount * WoffDirectoryEntrySize);
        if (WoffHeaderSize + directoryLength > declaredLength)
        {
            throw new FormatException("WOFF table directory is truncated.");
        }

        var records = new List<WoffTableRecord>(tableCount);
        var sfntOffset = checked(SfntHeaderSize + tableCount * SfntDirectoryEntrySize);
        for (var i = 0; i < tableCount; i++)
        {
            var recordOffset = WoffHeaderSize + i * WoffDirectoryEntrySize;
            var tag = ReadUInt(data, recordOffset);
            var sourceOffset = ReadUInt(data, recordOffset + 4);
            var compressedLength = ReadUInt(data, recordOffset + 8);
            var originalLength = ReadUInt(data, recordOffset + 12);
            var checksum = ReadUInt(data, recordOffset + 16);
            if (compressedLength > originalLength ||
                sourceOffset > declaredLength ||
                compressedLength > declaredLength - sourceOffset ||
                originalLength > int.MaxValue)
            {
                throw new FormatException("WOFF table bounds are invalid.");
            }

            records.Add(new WoffTableRecord(
                tag,
                checked((int)sourceOffset),
                checked((int)compressedLength),
                checked((int)originalLength),
                checksum,
                sfntOffset));
            sfntOffset = checked(Align4(sfntOffset + checked((int)originalLength)));
        }

        if (declaredSfntSize != sfntOffset)
        {
            throw new FormatException("WOFF uncompressed size does not match its table directory.");
        }

        var result = new byte[sfntOffset];
        WriteUInt(result, 0, flavor);
        WriteUShort(result, 4, tableCount);
        WriteSfntSearchParameters(result, tableCount);

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var directoryOffset = SfntHeaderSize + i * SfntDirectoryEntrySize;
            WriteUInt(result, directoryOffset, record.Tag);
            WriteUInt(result, directoryOffset + 4, record.Checksum);
            WriteUInt(result, directoryOffset + 8, checked((uint)record.TargetOffset));
            WriteUInt(result, directoryOffset + 12, checked((uint)record.OriginalLength));

            var target = result.AsSpan(record.TargetOffset, record.OriginalLength);
            if (record.CompressedLength == record.OriginalLength)
            {
                data.AsSpan(record.SourceOffset, record.OriginalLength).CopyTo(target);
                continue;
            }

            try
            {
                using var source = new MemoryStream(
                    data,
                    record.SourceOffset,
                    record.CompressedLength,
                    writable: false,
                    publiclyVisible: true);
                using var inflater = new ZLibStream(source, CompressionMode.Decompress, leaveOpen: false);
                inflater.ReadExactly(target);
                if (inflater.ReadByte() != -1)
                {
                    throw new FormatException("WOFF table expands beyond its declared size.");
                }
            }
            catch (InvalidDataException ex)
            {
                throw new FormatException("WOFF table compression data is invalid.", ex);
            }
            catch (EndOfStreamException ex)
            {
                throw new FormatException("WOFF table expands to fewer bytes than declared.", ex);
            }
        }

        return result;
    }

    private static void WriteSfntSearchParameters(byte[] data, ushort tableCount)
    {
        ushort powerOfTwo = 1;
        ushort entrySelector = 0;
        while (powerOfTwo <= tableCount / 2)
        {
            powerOfTwo *= 2;
            entrySelector++;
        }

        var searchRange = checked((ushort)(powerOfTwo * 16));
        WriteUShort(data, 6, searchRange);
        WriteUShort(data, 8, entrySelector);
        WriteUShort(data, 10, checked((ushort)(tableCount * 16 - searchRange)));
    }

    private static int Align4(int value) => checked((value + 3) & ~3);

    private static ushort ReadUShort(byte[] data, int offset)
    {
        if (offset < 0 || offset > data.Length - 2)
        {
            throw new FormatException("Font UInt16 value is truncated.");
        }

        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private static uint ReadUInt(byte[] data, int offset)
    {
        if (offset < 0 || offset > data.Length - 4)
        {
            throw new FormatException("Font UInt32 value is truncated.");
        }

        return (uint)((data[offset] << 24) |
                      (data[offset + 1] << 16) |
                      (data[offset + 2] << 8) |
                       data[offset + 3]);
    }

    private static void WriteUShort(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)(value >> 8);
        data[offset + 1] = (byte)value;
    }

    private static void WriteUInt(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }

    private readonly record struct WoffTableRecord(
        uint Tag,
        int SourceOffset,
        int CompressedLength,
        int OriginalLength,
        uint Checksum,
        int TargetOffset);
}
