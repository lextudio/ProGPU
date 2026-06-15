using System.Text;
using ProGPU.Text;
using Xunit;

namespace ProGPU.Tests;

public class SfntFontFaceTests
{
    [Fact]
    public void ReadsNamesFromSfntNameTable()
    {
        byte[] fontData = BuildSfnt("ProGPU Sans", "ProGPU Sans Regular");

        SfntFontFace face = SfntFontFace.Load(fontData);

        Assert.True(face.TryGetName(SfntNameIds.FamilyName, out string familyName));
        Assert.True(face.TryGetName(SfntNameIds.FullName, out string fullName));
        Assert.Equal("ProGPU Sans", familyName);
        Assert.Equal("ProGPU Sans Regular", fullName);
        Assert.True(face.TryGetTable("name", out ReadOnlyMemory<byte> nameTable));
        Assert.NotEqual(0, nameTable.Length);
    }

    [Fact]
    public void FontApiParsesFontInfoWithFaceIndex()
    {
        string file = Path.Combine(Path.GetTempPath(), $"progpu-font-{Guid.NewGuid():N}.ttf");
        File.WriteAllBytes(file, BuildSfnt("ProGPU Serif", "ProGPU Serif Bold"));

        try
        {
            FontInfo? info = FontApi.ParseFontInfo(file);

            Assert.NotNull(info);
            Assert.Equal("ProGPU Serif", info.FamilyName);
            Assert.Equal("ProGPU Serif Bold", info.Name);
            Assert.Equal(0, info.FaceIndex);
            Assert.Equal(file, info.FilePath);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void LoadsTrueTypeCollectionFaces()
    {
        byte[] fontData = BuildTtc(
            ("ProGPU Mono", "ProGPU Mono Regular"),
            ("ProGPU Mono", "ProGPU Mono Bold"));

        IReadOnlyList<SfntFontFace> faces = SfntFontFace.LoadFaces(fontData);

        Assert.Equal(2, faces.Count);
        Assert.Equal(0, faces[0].FaceIndex);
        Assert.Equal(1, faces[1].FaceIndex);
        Assert.True(faces[0].TryGetName(SfntNameIds.FullName, out string firstName));
        Assert.True(faces[1].TryGetName(SfntNameIds.FullName, out string secondName));
        Assert.Equal("ProGPU Mono Regular", firstName);
        Assert.Equal("ProGPU Mono Bold", secondName);
    }

    private static byte[] BuildSfnt(string familyName, string fullName)
    {
        byte[] nameTable = BuildNameTable(familyName, fullName);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteSfntFace(writer, 0, nameTable);
        return stream.ToArray();
    }

    private static byte[] BuildTtc(params (string FamilyName, string FullName)[] faces)
    {
        byte[][] nameTables = faces
            .Select(face => BuildNameTable(face.FamilyName, face.FullName))
            .ToArray();

        uint[] faceOffsets = new uint[faces.Length];
        uint offset = (uint)(12 + faces.Length * 4);
        for (int i = 0; i < faces.Length; i++)
        {
            faceOffsets[i] = offset;
            offset += (uint)(28 + nameTables[i].Length);
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        WriteTag(writer, "ttcf");
        WriteUInt(writer, 0x00010000);
        WriteUInt(writer, (uint)faces.Length);
        foreach (uint faceOffset in faceOffsets)
        {
            WriteUInt(writer, faceOffset);
        }

        for (int i = 0; i < faces.Length; i++)
        {
            stream.Position = faceOffsets[i];
            WriteSfntFace(writer, faceOffsets[i], nameTables[i]);
        }

        return stream.ToArray();
    }

    private static void WriteSfntFace(BinaryWriter writer, uint faceOffset, byte[] nameTable)
    {
        uint nameTableOffset = faceOffset + 28;
        WriteUInt(writer, 0x00010000);
        WriteUShort(writer, 1);
        WriteUShort(writer, 0);
        WriteUShort(writer, 0);
        WriteUShort(writer, 0);
        WriteTag(writer, "name");
        WriteUInt(writer, 0);
        WriteUInt(writer, nameTableOffset);
        WriteUInt(writer, (uint)nameTable.Length);
        writer.Write(nameTable);
    }

    private static byte[] BuildNameTable(string familyName, string fullName)
    {
        byte[] familyBytes = Encoding.BigEndianUnicode.GetBytes(familyName);
        byte[] fullBytes = Encoding.BigEndianUnicode.GetBytes(fullName);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        WriteUShort(writer, 0);
        WriteUShort(writer, 2);
        WriteUShort(writer, 30);
        WriteNameRecord(writer, SfntNameIds.FamilyName, familyBytes.Length, 0);
        WriteNameRecord(writer, SfntNameIds.FullName, fullBytes.Length, familyBytes.Length);
        writer.Write(familyBytes);
        writer.Write(fullBytes);

        return stream.ToArray();
    }

    private static void WriteNameRecord(BinaryWriter writer, ushort nameId, int length, int offset)
    {
        WriteUShort(writer, 3);
        WriteUShort(writer, 1);
        WriteUShort(writer, 0x0409);
        WriteUShort(writer, nameId);
        WriteUShort(writer, (ushort)length);
        WriteUShort(writer, (ushort)offset);
    }

    private static void WriteTag(BinaryWriter writer, string tag)
    {
        writer.Write(Encoding.ASCII.GetBytes(tag));
    }

    private static void WriteUShort(BinaryWriter writer, ushort value)
    {
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }

    private static void WriteUInt(BinaryWriter writer, uint value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)(value >> 16));
        writer.Write((byte)(value >> 8));
        writer.Write((byte)value);
    }
}
