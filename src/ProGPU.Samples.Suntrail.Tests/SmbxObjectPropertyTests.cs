using System.Text;
using ProGPU.Samples.Suntrail.Game.Import;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SmbxObjectPropertyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    public void LegacyPropertyChangesUseOnlyExistingVersionSlotsAndUndoExactly(int version)
    {
        byte[] bytes = SmbxLegacySourceTests.Fixture(version, 1, false);
        var editor = new SmbxGeometryEditor(SmbxSourceDocument.ReadLegacy(bytes, "legacy.lvl"));
        int npc = -1;
        for (int i = 0; i < editor.Geometry.Length; i++) if (editor.Geometry[i].Kind == SmbxGeometryKind.NpcAnchor) { npc = i; break; }
        Assert.True(npc >= 0); editor.Select(npc);
        var row = editor.Document.Records[editor.Geometry[npc].Record];
        if (row.TryGet("LR", out _))
        {
            editor.SetLayer("Sky path");
            Assert.Equal("Sky path", editor.Document.Records[editor.Geometry[npc].Record].Get("LR").GetString());
            editor.Undo(); Assert.Equal(bytes, editor.Document.WriteOriginal());
        }
        else
        {
            Assert.Throws<FormatException>(() => editor.SetLayer("Sky path"));
            Assert.Equal(bytes, editor.Document.WriteOriginal()); Assert.False(editor.CanUndo);
        }
    }

    [Theory]
    [InlineData(";")]
    [InlineData("")]
    [InlineData(";  ")]
    public void MissingLvlxPropertiesInsertAndUndoWithoutChangingOtherSourceText(string ending)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\uFEFFHEAD\r\nTL:\"Original property fixture\";\r\nHEAD_END\r\nNPC\r\nID:17;X:16;Y:32;UNKNOWN:[\"x\\;y\",2]" + ending + "\r\nNPC_END\r\n");
        var editor = new SmbxGeometryEditor(SmbxSourceDocument.ReadLvlx(bytes, "properties.lvlx")); editor.Select(0);
        editor.SetLayer("Cloud;\"orchard\"\\path");
        var row = editor.Document.Records[editor.Geometry[0].Record];
        Assert.Equal("Cloud;\"orchard\"\\path", row.Get("LR").GetString());
        Assert.Equal("[\"x\\;y\",2]", row.Get("UNKNOWN").RawValue); Assert.Equal(0, editor.Selected);
        byte[] assigned = editor.Document.WriteOriginal(); editor.SetNpcDirection(1);
        Assert.Equal(1, editor.Document.Records[editor.Geometry[0].Record].Get("D").GetInteger());
        editor.Undo(); Assert.Equal(assigned, editor.Document.WriteOriginal());
        editor.Undo(); Assert.Equal(bytes, editor.Document.WriteOriginal());
        editor.Redo(); editor.Redo(); Assert.Equal(1, editor.Document.Records[editor.Geometry[0].Record].Get("D").GetInteger());
    }

    [Fact]
    public void ExistingPropertiesAreNoOpsWhenSemanticallyUnchangedAndRejectUnsupportedEdits()
    {
        var editor = new SmbxGeometryEditor(SmbxSourceDocument.ReadLvlx(Encoding.UTF8.GetBytes(
            "HEAD\nHEAD_END\nBLOCK\nID:3;X:0;Y:0;W:32;H:32;LR:H416263;\nBLOCK_END\n"), "block.lvlx"));
        editor.Select(0); byte[] original = editor.Document.WriteOriginal(); editor.SetLayer("Abc");
        Assert.Equal(0, editor.Revision); Assert.False(editor.CanUndo); Assert.Equal(original, editor.Document.WriteOriginal());
        Assert.Throws<FormatException>(() => editor.SetNpcDirection(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetNpcDirection(2));
        Assert.Throws<FormatException>(() => editor.SetLayer(new string('a', 1025)));
        Assert.Equal(original, editor.Document.WriteOriginal()); editor.SetLayer("Changed");
        Assert.Equal(1, editor.Revision); editor.Undo(); Assert.Equal(original, editor.Document.WriteOriginal());
    }
}
