using System.Text;
using Microsoft.UI.Xaml;
using ProGPU.GameEngine.Assets;
using ProGPU.Samples.Suntrail.Game.Import;
using ProGPU.Samples.Suntrail.Presentation;
using ProGPU.Scene;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SmbxBoardArtworkTests
{
    private static (SmbxGeometryEditor Editor, SmbxLevelPack Pack) Prepare(string blockFields = "animated=false", int blockWidth = 2, int blockHeight = 2, bool sizableImage = false)
    {
        byte[] Text(string text) => Encoding.UTF8.GetBytes(text);
        var bundle = SmbxArtworkTests.Bundle(
            ("yard.lvlx", Text($"HEAD\nTL:\"Original sprite layout fixture\";\nHEAD_END\nBLOCK\nID:17;X:0;Y:0;W:{blockWidth};H:{blockHeight};\nBLOCK_END\nBGO\nID:18;X:10;Y:20;\nBGO_END\nNPC\nID:19;X:20;Y:30;D:1;\nNPC_END\n")),
            ("block-17.bmp", SmbxArtworkTests.Bmp(sizableImage ? 96 : 2, sizableImage ? 96 : 2, 40)),
            ("background-18.bmp", SmbxArtworkTests.Bmp(3, 6, 80)),
            ("npc-19.bmp", SmbxArtworkTests.Bmp(4, 8, 120)),
            ("npc-19.txt", Text("gfxwidth=4\ngfxheight=4\nframes=1\nframestyle=1\nwidth=2\nheight=2")));
        using var definitions = new SmbxConfigPack(SmbxArtworkTests.Bundle(
            ("main.ini", Text("[main]")),
            ("lvl_blocks.ini", Text("[blocks-main]\nconfig-dir=items")),
            ("lvl_bgo.ini", Text("[background-main]\nconfig-dir=items")),
            ("items/block-17.ini", Text("[block]\n" + blockFields)),
            ("items/background-18.ini", Text("[background]\nanimated=true\nframes=2"))), "main.ini");
        var pack = SmbxLevelPack.Open(bundle, "yard.lvlx", definitions);
        return (new(pack.Document), pack); // Prepared pack remains usable after definition disposal.
    }

    [Fact]
    public void KnownStaticVerticalAndDirectionalFramesHaveExplicitPlacement()
    {
        var (editor, pack) = Prepare(); var artwork = new SmbxBoardArtwork(editor, pack);
        Assert.Equal(3, artwork.Count); Assert.Empty(artwork.Issues.ToArray());
        Assert.Equal(new SmbxBounds(0, 0, 2, 2), artwork[0]!.Frame);
        Assert.Equal(new SmbxBounds(0, 0, 3, 3), artwork[1]!.Frame);
        Assert.Equal(new SmbxBounds(10, 20, 3, 3), artwork.Bounds(1, editor.Bounds(1)));
        Assert.Equal(new SmbxBounds(0, 4, 4, 4), artwork[2]!.Frame);
        Assert.Equal(new SmbxBounds(19, 28, 4, 4), artwork.Bounds(2, editor.Bounds(2)));
        Assert.True(editor.Geometry[2].IsAnchor); Assert.Equal(0d, editor.Geometry[2].Bounds.Width);
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(150, 110)]
    [InlineData(65_536, 96)]
    public void StaticSizableSheetRetainsOnePlacementAtItsSourceDimensions(int width, int height)
    {
        var (editor, pack) = Prepare("animated=false\nsizable=true", width, height, true);
        var layout = new SmbxBoardArtwork(editor, pack);
        Assert.True(layout[0]!.TiledNineSlice); Assert.Equal(3, layout.Count); Assert.Empty(layout.Issues.ToArray());
        Assert.Equal(new SmbxBounds(0, 0, 96, 96), layout[0]!.Frame);
        Assert.Equal(new SmbxBounds(0, 0, width, height), layout.Bounds(0, editor.Bounds(0)));
    }

    [Theory]
    [InlineData(63)]
    [InlineData(65_537)]
    public void UnsupportedSizableExtentRemainsAnEditableSourceRectangle(int width)
    {
        var (editor, pack) = Prepare("animated=false\nsizable=true", width, 96, true);
        var layout = new SmbxBoardArtwork(editor, pack);
        Assert.Null(layout[0]); Assert.Single(layout.Issues.ToArray()); Assert.Equal((double)width, editor.Bounds(0).Width);
    }

    [Theory]
    [InlineData("animated=false\nsizable=true", 2)]
    [InlineData("animated=true\nframes=3", 2)]
    [InlineData("", 2)]
    [InlineData("animated=false", 7)]
    public void UnresolvedOrResizedLayoutsKeepSourceGeometryWithoutInventingSprites(string fields, int width)
    {
        var (editor, pack) = Prepare(fields, width); byte[] original = editor.Document.WriteOriginal();
        var artwork = new SmbxBoardArtwork(editor, pack);
        Assert.Null(artwork[0]); Assert.Equal(2, artwork.Count); Assert.Single(artwork.Issues.ToArray());
        Assert.Equal(original, editor.Document.WriteOriginal()); Assert.Equal((double)width, editor.Geometry[0].Bounds.Width);
    }

    [Fact]
    public void SpritePickingAndDragPreviewUsePreparedPixelsWithoutChangingBodyAnchors()
    {
        var (editor, pack) = Prepare();
        var board = new SmbxSourceBoard(); board.Measure(new(600, 400)); board.Arrange(new Rect(0, 0, 600, 400));
        board.SetEditor(editor); board.SetArtwork(pack); board.ChangeZoom(100);
        var prepared = board.PreparedArtwork; byte[] original = editor.Document.WriteOriginal();
        Assert.Equal(2, board.HitTestObject(19.1, 28.1)); // Sprite corner outside the 10-screen-pixel source-anchor radius.
        editor.BeginMove(2); editor.Move(16, 0);
        Assert.Same(prepared, board.PreparedArtwork); Assert.Equal(original, editor.Document.WriteOriginal());
        Assert.Equal(2, board.HitTestObject(35.1, 28.1)); Assert.NotEqual(2, board.HitTestObject(19.1, 28.1));
        editor.CancelMove(); Assert.Same(prepared, board.PreparedArtwork); Assert.Equal(2, board.HitTestObject(19.1, 28.1));
        editor.BeginMove(2); editor.Move(16, 0); editor.CommitMove();
        Assert.NotSame(prepared, board.PreparedArtwork); Assert.Equal(2, board.HitTestObject(35.1, 28.1));
        editor.Undo(); Assert.Equal(original, editor.Document.WriteOriginal()); Assert.Equal(2, board.HitTestObject(19.1, 28.1));
        board.SetEditor(null); Assert.Null(board.PreparedArtwork); Assert.Equal(-1, board.HitTestObject(0, 0));
    }
}
