using System.Globalization;
using System.Text;
using ProGPU.Samples.Suntrail.Game.Import;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SmbxLegacySourceTests
{
    public static IEnumerable<object[]> Versions() => Enumerable.Range(0, 65).Select(version => new object[] { version });

    [Theory]
    [MemberData(nameof(Versions))]
    public void VersionedSectionsAndTrailingRecordsRemainAligned(int version)
    {
        byte[] bytes = Fixture(version, 1, false);
        var document = SmbxSourceDocument.ReadLegacy(bytes, "yard.lvl");
        Assert.True(document.IsLegacy); Assert.Equal(version, document.FormatVersion);
        Assert.Equal(bytes, document.WriteOriginal());
        var records = document.Records.ToArray();
        var sections = records.Where(r => r.Section == "SECTION").ToArray();
        Assert.Equal(version <= 7 ? 6 : 21, sections.Length);
        Assert.Equal(sections.Length - 1, sections[^1].IndexInSection);
        var block = Assert.Single(records, r => r.Section == "BLOCK");
        Assert.Equal(103, block.Get("H").GetInteger()); Assert.Equal(104, block.Get("W").GetInteger());
        Assert.Equal(1009, block.Get("LEGACY_CONTENT").GetInteger()); Assert.True(block.Get("IV").GetBoolean());
        Assert.Equal(version >= 61, block.TryGet("SL", out _));
        var npc = Assert.Single(records, r => r.Section == "NPC");
        Assert.Equal(301, npc.Get("X").GetInteger()); Assert.False(npc.TryGet("S1", out _));
        if (version >= 5) Assert.Equal("Line one\r\nline two \\ literal", npc.Get("MG").GetString());
        Assert.Equal(version >= 63, npc.TryGet("LA", out _));
        var door = Assert.Single(records, r => r.Section == "DOORS");
        Assert.Equal(404, door.Get("OY").GetInteger()); Assert.Equal(3, door.Get("ID").GetInteger());
        if (version >= 10)
        {
            var entry = Assert.Single(records, r => r.Section == "EVENTS_CLASSIC");
            Assert.Equal("event-one", entry.Get("ET").GetString());
            Assert.Equal("hide-19", entry.Get("LH19").GetString());
            Assert.Equal("", entry.Get("LH20").GetString());
            if (version >= 13) Assert.Equal(120, entry.Get("SM20").GetInteger());
            if (version >= 28) Assert.True(entry.Get("PC_RIGHT").GetBoolean());
            if (version >= 33) { Assert.Equal(-.5, entry.Get("AY").GetNumber()); Assert.Equal(2, entry.Get("AS").GetInteger()); }
        }
    }

    [Theory]
    [InlineData(14, 76, false, false)] [InlineData(15, 76, false, true)]
    [InlineData(29, 28, true, false)] [InlineData(30, 28, true, true)]
    [InlineData(64, 91, true, true)] [InlineData(64, 260, false, true)]
    [InlineData(64, 284, false, true)] [InlineData(64, 1, true, false)]
    public void ConditionalNpcAndGeneratorFieldsDoNotConsumeTheFollowingRecord(int version, int id, bool generator, bool special)
    {
        var source = SmbxSourceDocument.ReadLegacy(Fixture(version, id, generator), "conditions.lvl");
        var npc = Assert.Single(source.Records.ToArray(), r => r.Section == "NPC");
        Assert.Equal(id, npc.Get("ID").GetInteger()); Assert.Equal(special, npc.TryGet("S1", out _));
        Assert.Equal(id == 91, npc.TryGet("S2", out _));
        Assert.Equal(generator, npc.TryGet("GM", out var period));
        if (generator) Assert.Equal(55, period.GetInteger());
        Assert.Equal("Line one\r\nline two \\ literal", npc.Get("MG").GetString());
        Assert.Equal(401, Assert.Single(source.Records.ToArray(), r => r.Section == "DOORS").Get("IX").GetInteger());
    }

    [Fact]
    public void ExistingFieldEditsPreserveLegacySpellingAndRejectStructuralInjection()
    {
        var bytes = Fixture(64, 1, false);
        var source = SmbxSourceDocument.ReadLegacy(bytes, "edits.lvl");
        int block = Array.FindIndex(source.Records.ToArray(), r => r.Section == "BLOCK");
        string title = SmbxSourceDocument.EncodeLegacyString("New \\ title");
        var edited = source.WithFields([new(0, "TL", title), new(block, "X", "105"), new(block, "IV", "#FALSE#")]);
        Assert.Equal("New \\ title", edited.Records[0].Get("TL").GetString());
        Assert.Equal(105, edited.Records[block].Get("X").GetInteger()); Assert.False(edited.Records[block].Get("IV").GetBoolean());
        Assert.Equal(bytes, source.WriteOriginal()); Assert.True(edited.IsLegacy); Assert.Equal(64, edited.FormatVersion);
        Assert.Throws<FormatException>(() => source.WithFields([new(block, "X", "0\r\n\"next\"")]));
        Assert.Throws<FormatException>(() => source.WithFields([new(block, "IV", "1")]));
        Assert.Throws<FormatException>(() => SmbxSourceDocument.EncodeLegacyString("Cannot \"quote\""));
        Assert.Throws<FormatException>(() => source.WithFields([new(0, "TL", "\"bad\"\"quotes\"")]));
    }

    [Fact]
    public void TruncationVersionAndEncodingFailuresAreExplicit()
    {
        var bytes = Fixture(64, 1, false);
        foreach (int cut in new[] { 1, 15, bytes.Length / 2, bytes.Length - 4 })
            Assert.Throws<FormatException>(() => SmbxSourceDocument.ReadLegacy(bytes.AsSpan(0, cut), "cut.lvl"));
        Assert.Throws<FormatException>(() => SmbxSourceDocument.ReadLegacy("65\r\n"u8, "future.lvl"));
        Assert.Throws<FormatException>(() => SmbxSourceDocument.ReadLegacy([0xff], "encoding.lvl"));
        Assert.Throws<FormatException>(() => SmbxSourceDocument.ReadLegacy(new byte[SmbxSourceDocument.MaximumBytes + 1], "large.lvl"));
    }

    // Independently authored wire fixture from the specification's field tables.
    // Distinct dimensions/coordinates catch ordering mistakes; all text/art is ours.
    internal static byte[] Fixture(int version, int npcId, bool generator)
    {
        var lines = new List<string>();
        void N(params double[] values) { foreach (double value in values) lines.Add(value.ToString(CultureInfo.InvariantCulture)); }
        void S(params string[] values) { foreach (string value in values) lines.Add("\"" + value + "\""); }
        void B(params bool[] values) { foreach (bool value in values) lines.Add(value ? "#TRUE#" : "#FALSE#"); }
        N(version); if (version >= 17) N(5); if (version >= 60) S("Legacy format yard");
        for (int section = 0; section < (version <= 7 ? 6 : 21); section++)
        {
            N(-200000 + section * 20000, -200600 + section * 20000, -200000 + section * 20000, -199200 + section * 20000, 7, 123456);
            B(false, true); N(8); if (version >= 1) B(false); if (version >= 30) B(false); if (version >= 2) S("");
        }
        N(11, 12, 13, 14, 21, 22, 23, 24);
        N(101, 102, 103, 104, 63, 1009); B(true);
        if (version >= 61) B(false); if (version >= 10) S("Default"); if (version >= 14) S("", "", "");
        S("next"); N(201, 202, 125); if (version >= 10) S("Default"); S("next");
        N(301, 302, -1, npcId);
        if (npcId == 91) N(288, -1);
        else if (npcId is 260 or 284 || npcId == 76 && version >= 15 || npcId == 28 && version >= 30) N(2);
        if (version >= 3) { B(generator); if (generator) N(4, 2, 55); }
        if (version >= 5) S("Line one\r\nline two \\ literal");
        if (version >= 6) B(false, false); if (version >= 9) B(false);
        if (version >= 10) S("Default", "", "", ""); if (version >= 14) S(""); if (version >= 63) S("follow-me");
        S("next"); N(401, 402, 403, 404, 3, 1, 1);
        if (version >= 3) { S("other.lvl"); N(2); B(false); }
        if (version >= 4) { B(true); N(-1, -1); } if (version >= 7) N(4);
        if (version >= 12) { S("Default"); B(false); }
        if (version >= 23) B(true); if (version >= 25) B(false); if (version >= 26) B(false);
        if (version >= 10)
        {
            S("next");
            if (version >= 29) { N(501, 502, 503, 504, .5); if (version >= 62) B(true); S("Default"); }
            S("next", "Default"); B(false); S("next", "event-one");
            if (version >= 11) S("Event message"); if (version >= 14) N(7); if (version >= 18) N(2);
            for (int i = 0; i < 20; i++) { S($"hide-{i}", $"show-{i}"); if (version >= 14) S($"toggle-{i}"); }
            S("", ""); if (version >= 14) S("");
            if (version >= 13) for (int section = 0; section < 21; section++) N(100 + section, 200 + section, -1000 - section, -2000 - section, -3000 - section, -4000 - section);
            if (version >= 26) { S("follow-event"); N(15); } if (version >= 27) B(false);
            if (version >= 28) B(true, false, true, false, true, false, true, false, true, false);
            if (version >= 32) { B(false); S("moving-layer"); N(.25, -.25); } if (version >= 33) N(.5, -.5, 2);
        }
        return Encoding.UTF8.GetBytes("\ufeff" + string.Join("\r\n", lines) + "\r\n");
    }
}
