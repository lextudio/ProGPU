using System.Text;
using ProGPU.Samples.Suntrail.Game.Import;
using ProGPU.Samples.Suntrail.Presentation;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SmbxEditorLayersTests
{
    private static SmbxGeometryEditor Editor() => new(SmbxSourceDocument.ReadLvlx(Encoding.UTF8.GetBytes(
        "HEAD\nTL:\"Original layer fixture\";\nHEAD_END\nLAYERS\nLR:\"Upper\";HD:1;LC:1;\nLR:\"Empty\";\nLAYERS_END\nBLOCK\n" +
        "ID:1;X:0;Y:0;W:32;H:32;LR:\"Upper\";\nID:1;X:64;Y:0;W:32;H:32;LR:\"upper\";\n" +
        "ID:1;X:128;Y:0;W:32;H:32;\nID:1;X:192;Y:0;W:32;H:32;LR:\"\";\nBLOCK_END\n"), "layers.lvlx"));

    [Fact]
    public void DeclaredReferencedEmptyAndUnassignedGroupsKeepDistinctIdentity()
    {
        var editor = Editor(); byte[] original = editor.Document.WriteOriginal(); var layers = new SmbxEditorLayers(editor);
        Assert.Equal(5, layers.Layers.Length); Assert.Equal(0, layers.Layers[2].ObjectCount);
        Assert.Equal("Upper", layers.Layers[1].Name); Assert.Equal("upper", layers.Layers[3].Name);
        Assert.Null(layers.Layers[0].Name); Assert.Equal("", layers.Layers[4].Name);
        layers.SetVisible(1, false); Assert.False(layers.IsVisible(0)); Assert.True(layers.IsVisible(1));
        Assert.True(layers.IsVisible(2)); Assert.True(layers.IsVisible(3));
        Assert.Equal(original, editor.Document.WriteOriginal()); Assert.Equal(0, editor.Revision);
        layers.ShowAll(); Assert.True(layers.IsVisible(0)); // Source HD and LC are preserved, not executed.
    }

    [Fact]
    public void HiddenLayersCannotBePickedAndCommitUndoKeepViewChoices()
    {
        var editor = Editor(); var board = new SmbxSourceBoard(); board.SetEditor(editor);
        Assert.Equal(0, board.HitTestObject(8, 8)); editor.BeginMove(0); editor.Move(16, 0);
        board.SetLayerVisible(1, false); Assert.False(editor.IsMoving); Assert.Equal(-1, editor.Selected);
        Assert.Equal(-1, board.HitTestObject(8, 8)); Assert.Equal(1, board.HitTestObject(72, 8));
        editor.BeginMove(1); editor.Move(16, 0); editor.CommitMove();
        Assert.False(board.Layers!.IsVisible(0)); editor.Undo(); Assert.False(board.Layers!.IsVisible(0));
        board.ShowAllLayers(); Assert.Equal(0, board.HitTestObject(8, 8));
    }
}
