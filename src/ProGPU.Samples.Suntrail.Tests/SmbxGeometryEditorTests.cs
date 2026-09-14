using System.Text;
using ProGPU.Samples.Suntrail.Game.Import;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SmbxGeometryEditorTests
{
    private const string Yard = """
        HEAD
        TL:"Independent editor yard";
        HEAD_END
        SECTION
        SC:0;L:-200000;T:-200600;R:-198400;B:-200000;
        SECTION_END
        BLOCK
        ID:63;X:-199901;Y:-200100;W:96;H:32;CN:1001;UNKNOWN:[1,"keep\;this"];
        BLOCK_END
        NPC
        ID:999;X:-199801;Y:-200080;LA:"moving layer";
        NPC_END
        DOORS
        IX:-199701;IY:-200080;OX:-199201;OY:-200160;DT:2;LF:"unopened.lvl";
        DOORS_END
        SCRIPTS
        N:"Retained only";S:"never executed";
        SCRIPTS_END
        """;

    private static SmbxGeometryEditor Create(string text = Yard) => new(SmbxSourceDocument.ReadLvlx(
        Encoding.UTF8.GetBytes("\ufeff" + text.Replace("\n", "\r\n")), "yard.lvlx"));
    private static int Index(SmbxGeometryEditor editor, SmbxGeometryKind kind) =>
        Array.FindIndex(editor.Geometry.ToArray(), item => item.Kind == kind);

    [Fact]
    public void DragOnlyPreviewsUntilCommitAndUndoRestoresExactBytes()
    {
        var editor = Create(); var original = editor.Document; byte[] bytes = original.WriteOriginal();
        int index = Index(editor, SmbxGeometryKind.Block); var block = editor.Geometry[index];
        editor.BeginMove(index); editor.Move(19, -31);
        Assert.Same(original, editor.Document); Assert.False(editor.CanUndo);
        Assert.Equal(block.Bounds with { X = -199885, Y = -200132 }, editor.Bounds(index));
        editor.CommitMove(); Assert.True(editor.CanUndo); Assert.False(editor.IsMoving);
        string expected = Encoding.UTF8.GetString(bytes).Replace("X:-199901;Y:-200100", "X:-199885;Y:-200132");
        Assert.Equal(Encoding.UTF8.GetBytes(expected), editor.Document.WriteOriginal());
        editor.Undo(); Assert.Equal(bytes, editor.Document.WriteOriginal()); Assert.True(editor.CanRedo);
        editor.Redo(); Assert.Equal(Encoding.UTF8.GetBytes(expected), editor.Document.WriteOriginal());
        Assert.Equal("1001", editor.Document.Records[block.Record].Get("CN").RawValue);
        Assert.Equal("[1,\"keep\\;this\"]", editor.Document.Records[block.Record].Get("UNKNOWN").RawValue);
    }

    [Fact]
    public void CancelAndNoOpDoNotParseOrDirtyTheSource()
    {
        var editor = Create(); var source = editor.Document; int index = Index(editor, SmbxGeometryKind.Block);
        editor.BeginMove(index); editor.Move(32, 32); editor.CancelMove();
        Assert.Same(source, editor.Document); Assert.Equal(0, editor.Revision);
        editor.BeginMove(index); editor.Move(6, 7); editor.CommitMove();
        Assert.Same(source, editor.Document); Assert.False(editor.CanUndo);
        editor.BeginMove(index);
        for (int i = 0; i < 100; i++) editor.Move(i, i);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) editor.Move(i, i);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.Same(source, editor.Document);
    }

    [Fact]
    public void WarpEndpointsAndSectionBoundsUseTheirOwnSourceFields()
    {
        var editor = Create(); int exit = Index(editor, SmbxGeometryKind.WarpExit);
        int record = editor.Geometry[exit].Record;
        editor.BeginMove(exit); editor.Move(64, 32); editor.CommitMove();
        Assert.Equal(-199137, editor.Document.Records[record].Get("OX").GetInteger());
        Assert.Equal(-200128, editor.Document.Records[record].Get("OY").GetInteger());
        Assert.Equal(-199701, editor.Document.Records[record].Get("IX").GetInteger());
        Assert.Equal("unopened.lvl", editor.Document.Records[record].Get("LF").GetString());
        int section = Index(editor, SmbxGeometryKind.Section); var b = editor.Geometry[section].Bounds;
        editor.BeginMove(section); editor.Move(100, 40, 1); editor.CommitMove();
        Assert.Equal(b with { X = b.X + 100, Y = b.Y + 40 }, editor.Geometry[section].Bounds);
        // Moving a section frame edits its bounds only; it does not invent a
        // compound "move contents" operation that also alters actors or warps.
        Assert.Equal(-199137, editor.Document.Records[record].Get("OX").GetInteger());
    }

    [Fact]
    public void AnchorsPreserveUnknownObjectIdentityAndCannotAcquireInventedSizes()
    {
        var editor = Create(); int npc = Index(editor, SmbxGeometryKind.NpcAnchor);
        Assert.True(editor.Geometry[npc].IsAnchor); Assert.Equal("999", editor.Geometry[npc].SourceId);
        editor.Select(npc); Assert.Throws<FormatException>(() => editor.Resize(32, 32));
        Assert.Equal(npc, editor.HitTest(-199801, -200080, 12));
        int block = Index(editor, SmbxGeometryKind.Block);
        Assert.Equal(block, editor.HitTest(-199900, -200099, 12));
        editor.Select(block); editor.Resize(128, 48);
        Assert.Equal(128, editor.Geometry[block].Bounds.Width); Assert.Equal(48, editor.Geometry[block].Bounds.Height);
        editor.Undo(); Assert.Equal(96, editor.Geometry[block].Bounds.Width);
        var source = editor.Document;
        Assert.Throws<FormatException>(() => editor.Resize(double.NaN, 48)); Assert.Same(source, editor.Document);
    }

    [Theory]
    [InlineData(0)] [InlineData(14)] [InlineData(64)]
    public void LegacyMovesKeepLiteralFieldsAndFractionalCoordinates(int version)
    {
        var source = SmbxSourceDocument.ReadLegacy(SmbxLegacySourceTests.Fixture(version, 1, false), "yard.lvl");
        int row = Array.FindIndex(source.Records.ToArray(), r => r.Section == "BLOCK");
        source = source.WithFields([new(row, "X", "101.25")]);
        var editor = new SmbxGeometryEditor(source); int block = Index(editor, SmbxGeometryKind.Block);
        editor.BeginMove(block); editor.Move(32, -16); editor.CommitMove();
        Assert.Equal(133.25, editor.Document.Records[row].Get("X").GetNumber());
        Assert.Equal(103, editor.Document.Records[row].Get("H").GetInteger());
        Assert.Equal(104, editor.Document.Records[row].Get("W").GetInteger());
        editor.Undo(); Assert.Equal(source.WriteOriginal(), editor.Document.WriteOriginal());
    }

    [Fact]
    public void UnsupportedGeometryIsPreservedWithAnIssueAndNeverReinterpreted()
    {
        var editor = Create(Yard + "\nPHYSICS\nET:0;X:10;Y:10;W:50;H:-1;\nPHYSICS_END\n");
        Assert.Single(editor.Issues.ToArray()); var source = editor.Document;
        Assert.DoesNotContain(editor.Geometry.ToArray(), g => g.Kind == SmbxGeometryKind.Physics);
        int block = Index(editor, SmbxGeometryKind.Block); editor.BeginMove(block);
        Assert.Throws<FormatException>(() => editor.Move(double.MaxValue, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Move(double.NaN, 0));
        Assert.Same(source, editor.Document); Assert.Equal(editor.Geometry[block].Bounds, editor.Bounds(block));
    }

    [Fact]
    public void HistoryIsBoundedAndNewEditsDiscardTheRedoBranch()
    {
        var editor = Create(); int block = Index(editor, SmbxGeometryKind.Block);
        for (int i = 0; i < 70; i++) { editor.BeginMove(block); editor.Move(16, 0); editor.CommitMove(); }
        int undone = 0; while (editor.CanUndo) { editor.Undo(); undone++; }
        Assert.Equal(64, undone); Assert.True(editor.CanRedo);
        editor.BeginMove(block); editor.Move(0, 16); editor.CommitMove(); Assert.False(editor.CanRedo);
    }
}
