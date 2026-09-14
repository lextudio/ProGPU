using System.Text;
using ProGPU.Samples.Suntrail.Game.Import;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SmbxStructureTests
{
    [Fact]
    public void EmptyLvlxListsAreCreatedWithoutChangingExistingTextAndUndoRemovesThemExactly()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\ufeffHEAD\r\nTL:\"Independent blank\";UNKNOWN:[1,2];\r\nHEAD_END");
        var editor = new SmbxGeometryEditor(SmbxSourceDocument.ReadLvlx(bytes, "blank.lvlx"));
        foreach (var kind in new[] { SmbxGeometryKind.Block, SmbxGeometryKind.NpcAnchor, SmbxGeometryKind.BackgroundAnchor, SmbxGeometryKind.Physics, SmbxGeometryKind.WarpEntrance })
        {
            editor.Add(kind, 123, -199900, -200100);
            Assert.True(editor.Selected >= 0); Assert.Equal(kind, editor.Geometry[editor.Selected].Kind);
            Assert.StartsWith(Encoding.UTF8.GetString(bytes), Encoding.UTF8.GetString(editor.Document.WriteOriginal()));
            var added = editor.Document.WriteOriginal(); editor.Undo(); Assert.Equal(bytes, editor.Document.WriteOriginal());
            editor.Redo(); Assert.Equal(added, editor.Document.WriteOriginal());
            editor.DeleteSelected(); editor.Undo(); Assert.Equal(added, editor.Document.WriteOriginal());
            editor.Undo(); Assert.Equal(bytes, editor.Document.WriteOriginal());
        }
    }

    [Theory]
    [MemberData(nameof(SmbxLegacySourceTests.Versions), MemberType = typeof(SmbxLegacySourceTests))]
    public void LegacyInsertionAndDeletionKeepEveryVersionAligned(int version)
    {
        byte[] original = SmbxLegacySourceTests.Fixture(version, 1, false);
        var editor = new SmbxGeometryEditor(SmbxSourceDocument.ReadLegacy(original, "yard.lvl"));
        foreach (var kind in new[] { SmbxGeometryKind.Block, SmbxGeometryKind.BackgroundAnchor, SmbxGeometryKind.NpcAnchor, SmbxGeometryKind.WarpEntrance })
        {
            int records = editor.Document.Records.Length;
            editor.Add(kind, 28, -190000, -190000);
            Assert.Equal(records + 1, editor.Document.Records.Length); Assert.Equal(kind, editor.Geometry[editor.Selected].Kind);
            var afterInsert = editor.Document.WriteOriginal();
            editor.BeginMove(editor.Selected); editor.Move(32, 16); editor.CommitMove();
            var afterMove = editor.Document.WriteOriginal();
            editor.DeleteSelected(); Assert.Equal(records, editor.Document.Records.Length);
            editor.Undo(); Assert.Equal(afterMove, editor.Document.WriteOriginal());
            editor.Undo(); Assert.Equal(afterInsert, editor.Document.WriteOriginal());
            editor.Undo(); Assert.Equal(original, editor.Document.WriteOriginal());
        }
        if (version < 29)
        { Assert.Throws<FormatException>(() => editor.Add(SmbxGeometryKind.Physics, 1, 100, 100)); Assert.Equal(original, editor.Document.WriteOriginal()); }
        else
        {
            editor.Add(SmbxGeometryKind.Physics, 1, 100, 100);
            Assert.Equal(SmbxGeometryKind.Physics, editor.Geometry[editor.Selected].Kind);
            editor.Undo(); Assert.Equal(original, editor.Document.WriteOriginal());
        }
    }

    [Fact]
    public void LegacyNextStringInsideAMessageIsNeverUsedAsAnInsertionBoundary()
    {
        var source = SmbxSourceDocument.ReadLegacy(SmbxLegacySourceTests.Fixture(64, 1, false), "messages.lvl");
        int npc = Array.FindIndex(source.Records.ToArray(), r => r.Section == "NPC");
        source = source.WithFields([new(npc, "MG", SmbxSourceDocument.EncodeLegacyString("next"))]);
        var editor = new SmbxGeometryEditor(source); editor.Add(SmbxGeometryKind.NpcAnchor, 91, 100, 100);
        var npcs = editor.Document.Records.ToArray().Where(r => r.Section == "NPC").ToArray();
        Assert.Equal(2, npcs.Length); Assert.Equal("next", npcs[0].Get("MG").GetString());
        Assert.Equal(91, npcs[1].Get("ID").GetInteger()); Assert.Equal(0, npcs[1].Get("S1").GetInteger());
        editor.Undo(); Assert.Equal(source.WriteOriginal(), editor.Document.WriteOriginal());
    }

    [Fact]
    public void MixedFieldAndStructuralHistoryRestoresRecordIndicesAndUnknownData()
    {
        var source = SmbxSourceDocument.ReadLvlx(Encoding.UTF8.GetBytes("HEAD\nTL:\"Yard\";\nHEAD_END\nBLOCK\nID:63;X:100;Y:100;W:96;H:32;\nBLOCK_END\nNPC\nID:1;X:200;Y:200;FUTURE:\"retained\";\nNPC_END"), "mixed.lvlx");
        var editor = new SmbxGeometryEditor(source); int npc = Array.FindIndex(editor.Geometry.ToArray(), g => g.Kind == SmbxGeometryKind.NpcAnchor);
        editor.BeginMove(npc); editor.Move(32, 16); editor.CommitMove(); var moved = editor.Document.WriteOriginal();
        editor.Add(SmbxGeometryKind.Block, 2, 500, 500); var inserted = editor.Document.WriteOriginal();
        editor.Resize(128, 48); editor.DeleteSelected(); editor.Undo(); editor.Undo();
        Assert.Equal(inserted, editor.Document.WriteOriginal());
        editor.Undo(); Assert.Equal(moved, editor.Document.WriteOriginal());
        editor.Undo(); Assert.Equal(source.WriteOriginal(), editor.Document.WriteOriginal());
        editor.Redo(); editor.Redo(); Assert.Equal(inserted, editor.Document.WriteOriginal());
    }

    [Fact]
    public void WarpDeletionRemovesBothAnchorsAndFixedScaffoldingCannotBeDeleted()
    {
        var editor = new SmbxGeometryEditor(SmbxSourceDocument.CreateLvlx());
        for (int i = 0; i < editor.Geometry.Length; i++)
        { editor.Select(i); Assert.Throws<FormatException>(editor.DeleteSelected); }
        editor.Add(SmbxGeometryKind.WarpEntrance, 1, 100, 100); int count = editor.Geometry.Length;
        var warp = editor.Document.Records[editor.Geometry[editor.Selected].Record];
        Assert.Equal(1, warp.Get("DT").GetInteger()); Assert.Equal(3, warp.Get("ID").GetInteger()); Assert.Equal(3, warp.Get("OD").GetInteger());
        editor.Select(Array.FindIndex(editor.Geometry.ToArray(), g => g.Kind == SmbxGeometryKind.WarpExit)); editor.DeleteSelected();
        Assert.Equal(count - 2, editor.Geometry.Length); editor.Undo(); Assert.Equal(count, editor.Geometry.Length);
    }
}
