using System.Numerics;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using ProGPU.Scene;
using ProGPU.Samples.Suntrail.Game.Import;
using ProGPU.Samples.Suntrail.Presentation;
using Windows.Devices.Input;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SmbxWorkshopTests : IDisposable
{
    private readonly Application _app = Application.Current;
    private readonly ElementTheme _theme = ThemeManager.CurrentTheme;
    private readonly WindowInputState _input = InputSystem.Current;
    public SmbxWorkshopTests() => Application.Current = new App();
    public void Dispose() { InputSystem.Current = _input; Application.Current = _app; ThemeManager.CurrentTheme = _theme; }

    private const string Yard = """
        HEAD
        TL:"Original workshop fixture";
        HEAD_END
        SECTION
        SC:0;L:-200000;T:-200600;R:-198400;B:-200000;
        SECTION_END
        BLOCK
        ID:63;X:-199700;Y:-200400;W:128;H:64;CN:0;FUTURE:"retained";
        BLOCK_END
        NPC
        ID:789;X:-199300;Y:-200400;GE:0;
        NPC_END
        """;
    private static SmbxSourceDocument Source() => SmbxSourceDocument.ReadLvlx(Encoding.UTF8.GetBytes(Yard), "original.lvlx");

    [Fact]
    public void PackageSelectionStagesWithoutDiscardAndMapSwitchRetainsEditorUndo()
    {
        var view = new GameView(); InputSystem.Current = InputSystem.CreateExternalState(view);
        void Layout() { view.Measure(new(1440, 900)); view.Arrange(new Rect(0, 0, 1440, 900)); }
        SmbxWorkshop? activeWorkshop = null;
        void Activate(string name)
        {
            Descendants(name == "SMBX workshop" ? view : activeWorkshop ?? (FrameworkElement)view).OfType<Button>().First(b => b.Content is TextBlock t && t.Text.StartsWith(name, StringComparison.Ordinal))
                .OnKeyDown(new() { Key = Silk.NET.Input.Key.Enter }); Layout();
        }
        Layout(); Activate("Level workshop"); Activate("SMBX workshop");
        var workshop = Assert.Single(Descendants(view).OfType<SmbxWorkshop>());
        activeWorkshop = workshop;
        workshop.LoadDocument(Source()); var previous = workshop.Editor!;
        previous.Add(SmbxGeometryKind.Block, 1, 0, 0);
        workshop.LoadPackage(SmbxPackageWorkshopTests.Package()); Layout();
        Assert.Same(previous, workshop.Editor); Assert.Null(workshop.Package);
        var picker = Assert.Single(Descendants(workshop).OfType<ComboBox>(), c => AutomationProperties.GetName(c) == "SMBX package level");
        Assert.Null(picker.SelectedItem);
        picker.SelectedItem = picker.Items[0]; Activate("Open selected level");
        Assert.Same(previous, workshop.Editor); // First activation exposes the discard action.
        Activate("Discard drafts & open");
        var first = workshop.Editor!; Assert.NotSame(previous, first);
        first.Add(SmbxGeometryKind.Block, 99, 256, 512); byte[] changed = first.Document.WriteOriginal();
        picker.SelectedItem = picker.Items[1]; Activate("Open selected level");
        Assert.NotSame(first, workshop.Editor); Assert.True(workshop.HasUnsavedChanges);
        picker.SelectedItem = picker.Items[0]; Activate("Open selected level");
        Assert.Same(first, workshop.Editor); Assert.Equal(changed, first.Document.WriteOriginal());
        Activate("Undo"); Assert.True(first.CanRedo);
        Activate("← Workshop"); Activate("SMBX workshop"); Assert.Same(first, workshop.Editor);
    }

    [Theory]
    [InlineData(PointerDeviceType.Mouse)]
    [InlineData(PointerDeviceType.Touch)]
    public void InGameSourceWorkshopDragsAtGlobalCoordinatesAndRetainsTheDraftWhenClosed(PointerDeviceType device)
    {
        var view = new GameView(); InputSystem.Current = InputSystem.CreateExternalState(view);
        void Layout() { view.Measure(new(1440, 900)); view.Arrange(new Rect(0, 0, 1440, 900)); }
        Button Button(string name) => Descendants(view).OfType<Button>().First(b => b.Content is TextBlock t && t.Text.StartsWith(name, StringComparison.Ordinal));
        void Activate(string name) { Button(name).OnKeyDown(new() { Key = Silk.NET.Input.Key.Enter }); Layout(); }
        Layout(); Activate("Level workshop"); Activate("SMBX workshop");
        var workshop = Assert.Single(Descendants(view).OfType<SmbxWorkshop>());
        workshop.LoadDocument(Source()); Layout(); workshop.Board.Fit();
        var editor = workshop.Editor!; var board = workshop.Board;
        int block = Array.FindIndex(editor.Geometry.ToArray(), g => g.Kind == SmbxGeometryKind.Block);
        var bounds = editor.Geometry[block].Bounds; var source = editor.Document;
        Vector2 Screen(double x, double y) => Vector2.Transform(board.ScreenPoint(x, y), board.GetGlobalCoordinateTransformMatrix());
        void Send(PointerInputKind kind, Vector2 p, uint id = 73) => InputSystem.InjectPointer(new(kind, id, device, p, 1_000_000,
            IsInContact: kind is PointerInputKind.Pressed or PointerInputKind.Moved,
            IsLeftButtonPressed: device == PointerDeviceType.Mouse && kind is PointerInputKind.Pressed or PointerInputKind.Moved));
        var start = Screen(bounds.X + 32, bounds.Y + 32); var end = start + new Vector2((float)(48 * board.Zoom), (float)(-32 * board.Zoom));
        Assert.Same(board, InputSystem.HitTest(start));
        Send(PointerInputKind.Pressed, start); Send(PointerInputKind.Moved, end);
        Assert.True(editor.IsMoving); Assert.Same(source, editor.Document);
        Send(PointerInputKind.Moved, start + new Vector2(100, 100), 74); // A second finger must not own this drag.
        Send(PointerInputKind.Released, end); Assert.False(editor.IsMoving);
        Assert.Equal(bounds with { X = bounds.X + 48, Y = bounds.Y - 32 }, editor.Geometry[block].Bounds);
        Assert.True(workshop.HasUnsavedChanges);
        byte[] changed = editor.Document.WriteOriginal();
        Activate("← Workshop"); Assert.Equal(Visibility.Collapsed, workshop.Visibility);
        Activate("SMBX workshop"); Assert.Same(editor, workshop.Editor); Assert.Equal(changed, editor.Document.WriteOriginal());
        editor.Undo(); Assert.Equal(source.WriteOriginal(), editor.Document.WriteOriginal());
        start = Screen(bounds.X + 32, bounds.Y + 32);
        Send(PointerInputKind.Pressed, start); Send(PointerInputKind.Moved, start + new Vector2(64, 16)); Send(PointerInputKind.Canceled, start);
        Assert.Equal(source.WriteOriginal(), editor.Document.WriteOriginal()); Assert.False(editor.IsMoving);
    }

    [Fact]
    public void PanZoomAndFileReplacementKeepSourceDocumentsIndependent()
    {
        var board = new SmbxSourceBoard(); board.Measure(new(900, 500)); board.Arrange(new Rect(0, 0, 900, 500));
        var first = new SmbxGeometryEditor(Source()); board.SetEditor(first);
        var point = board.ScreenPoint(-199700, -200400); var world = board.SourcePoint(point);
        Assert.InRange(Math.Abs(world.X + 199700), 0, .001); Assert.InRange(Math.Abs(world.Y + 200400), 0, .001);
        double camera = board.CameraX; board.Pan(90, 0); Assert.True(board.CameraX > camera);
        double zoom = board.Zoom; board.ChangeZoom(1.25); Assert.Equal(zoom * 1.25, board.Zoom);
        Assert.Equal(Source().WriteOriginal(), first.Document.WriteOriginal());
        var second = new SmbxGeometryEditor(Source()); board.SetEditor(second);
        first.BeginMove(1); first.Move(32, 0); first.CommitMove();
        Assert.Equal(Source().WriteOriginal(), second.Document.WriteOriginal());
        board.ChangeZoom(double.NaN); Assert.True(double.IsFinite(board.Zoom));
    }

    [Theory]
    [InlineData(PointerDeviceType.Mouse)]
    [InlineData(PointerDeviceType.Touch)]
    public void PaletteDropAndTapCreateOriginalFormatObjectsAndCancellationKeepsTheSource(PointerDeviceType device)
    {
        var view = new GameView(); InputSystem.Current = InputSystem.CreateExternalState(view);
        void Layout() { view.Measure(new(1440, 900)); view.Arrange(new Rect(0, 0, 1440, 900)); }
        Button Find(FrameworkElement root, string text) => Descendants(root).OfType<Button>().First(b =>
            (b.Content is TextBlock label ? label.Text : b.Content as string)?.StartsWith(text, StringComparison.Ordinal) == true);
        Layout(); Find(view, "Level workshop").OnKeyDown(new() { Key = Silk.NET.Input.Key.Enter }); Layout();
        Find(view, "SMBX workshop").OnKeyDown(new() { Key = Silk.NET.Input.Key.Enter }); Layout();
        var workshop = Assert.Single(Descendants(view).OfType<SmbxWorkshop>());
        Find(workshop, "New LVLX").OnKeyDown(new() { Key = Silk.NET.Input.Key.Enter }); Layout();
        var editor = workshop.Editor!; var board = workshop.Board; board.Fit();
        var id = Assert.Single(Descendants(workshop).OfType<TextBox>(), t => AutomationProperties.GetName(t) == "SMBX palette object ID"); id.Text = "17";
        var palette = Find(workshop, "Block"); byte[] original = editor.Document.WriteOriginal(); int count = editor.Document.Records.Length;
        Vector2 Screen(double x, double y) => Vector2.Transform(board.ScreenPoint(x, y), board.GetGlobalCoordinateTransformMatrix());
        void Send(PointerInputKind kind, Vector2 p) => InputSystem.InjectPointer(new(kind, 83, device, p, 1_000_000,
            IsInContact: kind is PointerInputKind.Pressed or PointerInputKind.Moved,
            IsLeftButtonPressed: device == PointerDeviceType.Mouse && kind is PointerInputKind.Pressed or PointerInputKind.Moved));
        var start = Vector2.Transform(palette.Size / 2, palette.GetGlobalCoordinateTransformMatrix());
        Send(PointerInputKind.Pressed, start); Send(PointerInputKind.Moved, Screen(512, 512));
        Assert.Equal(original, editor.Document.WriteOriginal());
        Send(PointerInputKind.Released, Screen(512, 512)); Assert.Equal(count + 1, editor.Document.Records.Length);
        var block = editor.Document.Records[editor.Geometry[editor.Selected].Record];
        Assert.Equal(17, block.Get("ID").GetInteger()); Assert.Equal(512, block.Get("X").GetInteger()); Assert.Equal(512, block.Get("Y").GetInteger());
        Send(PointerInputKind.Pressed, Screen(800, 512)); Send(PointerInputKind.Released, Screen(800, 512));
        Assert.Equal(count + 2, editor.Document.Records.Length);
        var beforeCancel = editor.Document.WriteOriginal();
        Send(PointerInputKind.Pressed, Screen(900, 512)); Send(PointerInputKind.Canceled, Screen(900, 512));
        Assert.Equal(beforeCancel, editor.Document.WriteOriginal());
        editor.Undo(); editor.Undo(); Assert.Equal(original, editor.Document.WriteOriginal());
    }

    private static IEnumerable<FrameworkElement> Descendants(FrameworkElement root)
    {
        yield return root;
        foreach (var child in root.Children.OfType<FrameworkElement>())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
