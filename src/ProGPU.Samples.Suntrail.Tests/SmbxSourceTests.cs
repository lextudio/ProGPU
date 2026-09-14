using System.Text;
using ProGPU.Samples.Suntrail.Game.Import;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SmbxSourceTests
{
    private const string Fixture = """
        HEAD
        TL:"Format yard";SZ:0;XTRA:"{\"independent\"\:true}";
        HEAD_END
        SECTION
        SC:0;L:-200000;T:-200600;R:-198400;B:-200000;OE:1;BG:0;
        SECTION_END
        STARTPOINT
        ID:1;X:-199940;Y:-200100;D:1;
        STARTPOINT_END
        BLOCK
        ID:63;X:-200000;Y:-200064;W:1600;H:64;LR:"Default";FUTURE:[1,"a\;b",H636174];
        BLOCK_END
        EVENTS
        N:"Optional action";
        EVENT
        N:"Nested action";VALUE:[1,[2,3],"escaped\"quote"];
        EVENT_END
        EVENTS_END
        SCRIPTS
        N:"Unexecuted";L:0;S:"anything\nincluding\:text";
        SCRIPTS_END
        """;

    [Fact]
    public void ReaderOwnsExactSourceAndRetainsUnknownFieldsAndNestedSections()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\ufeff" + Fixture.Replace("\n", "\r\n"));
        var expected = (byte[])bytes.Clone();
        var document = SmbxSourceDocument.ReadLvlx(bytes, "yard.lvlx");
        Array.Fill(bytes, (byte)0);
        Assert.Equal(expected, document.WriteOriginal());
        var output = document.WriteOriginal(); Array.Fill(output, (byte)0);
        Assert.Equal(expected, document.WriteOriginal());
        Assert.Equal(7, document.Records.Length);
        Assert.Equal("Format yard", document.Records[0].Get("TL").GetString());
        Assert.Equal("{\"independent\":true}", document.Records[0].Get("XTRA").GetString());
        Assert.True(document.Records[1].Get("OE").GetBoolean());
        Assert.Equal(-200000, document.Records[1].Get("L").GetInteger());
        Assert.Equal("[1,\"a\\;b\",H636174]", document.Records[3].Get("FUTURE").RawValue);
        Assert.Equal("EVENTS/EVENT", document.Records[5].SectionPath);
        Assert.Equal("anything\nincluding:text", document.Records[6].Get("S").GetString());
    }

    [Fact]
    public void FieldEditsPreserveAllOtherBytesAndReparseTransactionally()
    {
        var input = "\ufeff" + Fixture.Replace("\n", "\r\n");
        var document = SmbxSourceDocument.ReadLvlx(Encoding.UTF8.GetBytes(input), "yard.lvlx");
        string title = SmbxSourceDocument.EncodeString("Łódź; \"new\" [yard] 100%\nsecond line");
        var edited = document.WithFields([new(0, "TL", title), new(3, "X", "-199968")]);
        string expected = input.Replace("TL:\"Format yard\"", "TL:" + title)
            .Replace("ID:63;X:-200000", "ID:63;X:-199968");
        Assert.Equal(Encoding.UTF8.GetBytes(expected), edited.WriteOriginal());
        Assert.Equal(Encoding.UTF8.GetBytes(input), document.WriteOriginal());
        Assert.Equal("Łódź; \"new\" [yard] 100%\nsecond line", edited.Records[0].Get("TL").GetString());
        Assert.Equal(-199968, edited.Records[3].Get("X").GetInteger());
        Assert.Same(document, document.WithFields([]));
        Assert.Throws<FormatException>(() => document.WithFields([new(3, "X", "0;ID:1")]));
        Assert.Throws<FormatException>(() => document.WithFields([new(3, "X", "0"), new(3, "X", "1")]));
        Assert.Throws<FormatException>(() => document.WithFields([new(3, "MISSING", "0")]));
        Assert.Equal(Encoding.UTF8.GetBytes(input), document.WriteOriginal());
    }

    [Theory]
    [InlineData("HEAD\nTL:\"bad\\q\"\nHEAD_END")]
    [InlineData("HEAD\nTL:\"unterminated\nHEAD_END")]
    [InlineData("HEAD\nTL:\"valid\";TL:\"duplicate\"\nHEAD_END")]
    [InlineData("HEAD\nTL:\"valid\"\nBLOCK_END")]
    [InlineData("HEAD\nTL:\"valid\"\n")]
    [InlineData("HEAD\nV:[1,]\nHEAD_END")]
    [InlineData("HEAD\nV:[1,2]]\nHEAD_END")]
    [InlineData("HEAD\nV:NaN\nHEAD_END")]
    [InlineData("HEAD\nV:H123\nHEAD_END")]
    [InlineData("HEAD\nV:\nHEAD_END")]
    [InlineData("HEAD\nHEAD_END\nV:1")]
    public void MalformedSyntaxProducesAnExplicitLineError(string source)
    {
        var error = Assert.Throws<FormatException>(() => SmbxSourceDocument.ReadLvlx(Encoding.UTF8.GetBytes(source), "bad.lvlx"));
        Assert.Contains("LVLX line", error.Message);
    }

    [Fact]
    public void HexStringsBooleanArraysAndNumbersRetainTheirDistinctRepresentations()
    {
        string bits = new('1', 400);
        var source = SmbxSourceDocument.ReadLvlx(Encoding.UTF8.GetBytes($"HEAD\nTL:Hc581c3b364c5ba;FLAGS:{bits};MASK:B12FD24;N:-1.25e2;\nHEAD_END"), "types.lvlx");
        Assert.Equal("Łódź", source.Records[0].Get("TL").GetString());
        Assert.Equal(bits, source.Records[0].Get("FLAGS").RawValue);
        Assert.Equal(-125, source.Records[0].Get("N").GetNumber());
        Assert.Throws<FormatException>(() => source.Records[0].Get("N").GetInteger());
        Assert.Throws<FormatException>(() => source.Records[0].Get("N").GetBoolean());
        Assert.Throws<FormatException>(() => new SmbxSourceField("ID", "2147483647.00000001", 0).GetInteger());
        Assert.Throws<FormatException>(() => new SmbxSourceField("ID", "1e-100", 0).GetInteger());
    }

    [Fact]
    public void ResourceLimitsRejectBeforeBuildingAnUnboundedDocument()
    {
        Assert.Throws<FormatException>(() => SmbxSourceDocument.ReadLvlx(new byte[SmbxSourceDocument.MaximumBytes + 1], "large.lvlx"));
        Assert.Throws<FormatException>(() => SmbxSourceDocument.ReadLvlx([0xff, 0xfe], "encoding.lvlx"));
        string fields = string.Join(';', Enumerable.Range(0, 257).Select(i => $"F{i}:0"));
        Assert.Throws<FormatException>(() => SmbxSourceDocument.ReadLvlx(Encoding.UTF8.GetBytes("HEAD\n" + fields + "\nHEAD_END"), "fields.lvlx"));
        string nested = "HEAD\nV:" + new string('[', 17) + "0" + new string(']', 17) + "\nHEAD_END";
        Assert.Throws<FormatException>(() => SmbxSourceDocument.ReadLvlx(Encoding.UTF8.GetBytes(nested), "arrays.lvlx"));
    }
}
