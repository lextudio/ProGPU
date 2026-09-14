using System.Numerics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using ProGPU.Fonts.Inter;
using ProGPU.Samples.Suntrail.Game.Import;
using ProGPU.Vector;
using ProGPU.GameEngine.Assets;

namespace ProGPU.Samples.Suntrail.Presentation;

/// <summary>Shared source-format authoring surface; file access is through the host's picker.</summary>
public sealed partial class SmbxWorkshop : Grid
{
    public SmbxGeometryEditor? Editor { get; private set; }
    public SmbxSourceBoard Board { get; } = new();
    public event Action? CloseRequested;
    private readonly TextBlock _title, _selection, _status;
    private readonly SmbxArtworkPreview _artworkPreview = new();
    private readonly TextBlock _artworkCaption;
    private readonly Button _open, _new, _undo, _redo, _pan;
    private readonly List<Button> _editing = [];
    private bool _busy, _replaceRequested, _newRequested;
    private int _savedRevision;
    public bool HasUnsavedChanges => Package?.HasUnsavedChanges ?? (Editor is { } editor && editor.Revision != _savedRevision);

    public SmbxWorkshop(Func<string, Action, bool, Button> createButton)
    {
        _status = Label("Open an LVL or LVLX file. No scripts or external files are executed.", 13);
        Name = "SmbxWorkshop";
        Background = new ThemeResourceBrush("SuntrailInk");
        RowDefinitions.Add(GridLength.Auto); RowDefinitions.Add(new GridLength(1, GridUnitType.Star)); RowDefinitions.Add(GridLength.Auto);
        var header = new StackPanel { Spacing = 8, Margin = new Thickness(16, 12, 16, 8) };
        _title = Label("SMBX WORKSHOP", 21); header.AddChild(_title);
        header.AddChild(Label("Edit original levels and inspect supplied sprites. Geometry playtest uses Suntrail movement; full SMBX behavior remains in development.", 13));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        Button Action(string title, Action action, bool edit = true)
        {
            var button = createButton(title, () =>
            {
                if (_busy) return;
                try { action(); } catch (Exception e) when (e is FormatException or ArgumentOutOfRangeException) { _status.Text = e.Message; }
            }, false);
            button.Padding = new Thickness(12, 8, 12, 8); button.MinHeight = 42;
            actions.AddChild(button); if (edit) _editing.Add(button); return button;
        }
        Action("← Workshop", () => { Board.Cancel(); CloseRequested?.Invoke(); }, false);
        _open = Action("Open SMBX…", RequestOpen, false);
        _new = Action("New LVLX", RequestNew, false);
        Action("Geometry playtest →", () => StartPlaytest(createButton));
        Action("Load definitions…", () => _ = OpenDefinitionsAsync(), false);
        Action("Refresh sprites", () => { Board.Cancel(); RefreshCurrentArtwork(); ReportArtwork(); });
        Action("Save copy…", () => _ = SaveAsync());
        _undo = Action("Undo", () => Editor?.Undo()); _redo = Action("Redo", () => Editor?.Redo());
        Action("Select", () => { Board.SetTool(null); Refresh(); });
        _pan = Action("Drag: select", () => { bool pan = !Board.PanMode; Board.SetTool(null); Board.PanMode = pan; Refresh(); });
        Action("Delete object", () => { Board.Cancel(); Editor?.DeleteSelected(); });
        Action("Zoom −", () => Board.ChangeZoom(.8)); Action("Zoom +", () => Board.ChangeZoom(1.25));
        Action("Fit", Board.Fit); Action("Next section", Board.NextSection);
        Action("Width −", () => Resize(-16, 0)); Action("Width +", () => Resize(16, 0));
        Action("Height −", () => Resize(0, -16)); Action("Height +", () => Resize(0, 16));
        header.AddChild(new ScrollViewer { Content = actions, Height = 56, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        header.AddChild(CreatePackageActions(createButton));
        _selection = Label("Drag objects to move them on a 16-unit grid. Drag empty space or use pan mode to move the view.", 13);
        header.AddChild(_selection); AddChild(header);
        _definitionInfo = Label("Base definitions: none loaded.", 12); header.AddChild(_definitionInfo);
        var workspace = new Grid(); workspace.ColumnDefinitions.Add(new GridLength(128)); workspace.ColumnDefinitions.Add(new GridLength(1, GridUnitType.Star));
        var palette = new StackPanel { Spacing = 6, Margin = new Thickness(10, 0, 8, 0) };
        palette.AddChild(Label("OBJECT ID", 12));
        var idEntry = new TextBox { Text = "1", MinHeight = 38, Font = InterFontFamily.Regular, FontSize = 15 };
        AutomationProperties.SetName(idEntry, "SMBX palette object ID"); palette.AddChild(idEntry);
        int ObjectId() => int.TryParse(idEntry.Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int id) && id > 0
            ? id : throw new FormatException("Enter a positive integer object ID.");
        foreach (var (kind, label) in new[] { (SmbxGeometryKind.Block, "Block"), (SmbxGeometryKind.BackgroundAnchor, "Background"),
            (SmbxGeometryKind.NpcAnchor, "NPC"), (SmbxGeometryKind.Physics, "Water zone"), (SmbxGeometryKind.WarpEntrance, "Pipe warp") })
        {
            var button = new PaletteButton(Board, kind, ObjectId, message => _status.Text = message)
            {
                Content = label, Font = InterFontFamily.Regular, FontSize = 14, MinHeight = 40, Padding = new Thickness(8),
                Background = new ThemeResourceBrush("SuntrailButton"), Foreground = new ThemeResourceBrush("SuntrailCream")
            };
            AutomationProperties.SetName(button, "Place SMBX " + label); palette.AddChild(button); _editing.Add(button);
        }
        palette.AddChild(Label("Drag onto map\nor select, then tap.", 12));
        palette.AddChild(_artworkPreview);
        _artworkCaption = Label("Select source art", 12); palette.AddChild(_artworkCaption);
        var nextFrame = createButton("Art frame +", () => { _artworkPreview.NextFrame(); _artworkCaption.Text = _artworkPreview.Description; }, false);
        palette.AddChild(nextFrame); _editing.Add(nextFrame);
        palette.AddChild(CreateLayerControls(createButton));
        palette.AddChild(CreatePropertyControls(createButton));
        palette.AddChild(CreateWarpControls(createButton));
        workspace.AddChild(new ScrollViewer { Content = palette, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        SetColumn(Board, 1); workspace.AddChild(Board); SetRow(workspace, 1); AddChild(workspace);
        _status.Margin = new Thickness(16, 10, 16, 12); SetRow(_status, 2); AddChild(_status);
        Board.Notice += message => { _status.Text = message; Refresh(); };
        Refresh();
    }

    private static TextBlock Label(string text, float size) => new()
    { Text = text, Font = InterFontFamily.Regular, FontSize = size, Foreground = new ThemeResourceBrush("SuntrailCream") };
    private static void ButtonText(Button button, string text)
    { if (button.Content is TextBlock label && label.Text != text) label.Text = text; }

    public void LoadDocument(SmbxSourceDocument document)
    {
        var next = new SmbxGeometryEditor(document); // Failure leaves the previous editor available.
        var artwork = Definitions is null ? null : SmbxLevelPack.Prepare(EmptyArtworkBundle, document, Definitions);
        Package = null; ClearPackagePicker();
        _standaloneArtwork = artwork;
        AttachEditor(next);
    }

    private void AttachEditor(SmbxGeometryEditor? next)
    {
        if (Package is not null) _standaloneArtwork = null;
        Board.Cancel(); if (Editor is { } old) old.Changed -= Refresh;
        Editor = next; _savedRevision = Package?.Current?.SavedRevision ?? next?.Revision ?? 0;
        _replaceRequested = _newRequested = false;
        Board.SetEditor(next, CurrentArtwork);
        if (next is null) { _title.Text = "SMBX WORKSHOP"; _selection.Text = "Choose a level from the package."; Refresh(); return; }
        next.Changed += Refresh;
        _status.Text = next.Issues.Length == 0 ? "Source loaded. Changes affect geometry fields only." :
            $"{next.Issues.Length} geometry records need attention. First: {next.Issues[0].Message} All source data is retained.";
        Refresh();
    }

    private void Refresh()
    {
        foreach (var button in _editing) button.IsEnabled = !_busy && Editor is not null;
        _open.IsEnabled = _new.IsEnabled = !_busy; Board.IsEnabled = !_busy;
        _undo.IsEnabled = !_busy && Editor is { CanUndo: true }; _redo.IsEnabled = !_busy && Editor is { CanRedo: true };
        RefreshPackageActions();
        RefreshArtworkPreview();
        RefreshDefinitionInfo();
        RefreshLayers();
        RefreshProperties();
        RefreshWarpProperties();
        ButtonText(_pan, Board.PanMode ? "Drag: pan" : Board.Tool is { } tool ? "Place: " + tool : "Drag: select");
        ButtonText(_open, _replaceRequested ? "Discard edits & open…" : "Open SMBX…");
        ButtonText(_new, _newRequested ? "Discard edits & new" : "New LVLX");
        if (Editor is not { } editor) return;
        bool currentDirty = Package?.Current?.HasUnsavedChanges ?? HasUnsavedChanges;
        _title.Text = $"SMBX WORKSHOP · {Path.GetFileName(editor.Document.FileName)}{(currentDirty ? " · unsaved" : "")}";
        if (editor.Selected >= 0)
        {
            var item = editor.Geometry[editor.Selected]; var b = editor.Bounds(editor.Selected);
            string identity = item.SourceId.Length > 0 ? " ID " + item.SourceId : "";
            _selection.Text = FormattableString.Invariant($"{item.Kind}{identity}  ·  X {b.X:0.###}  Y {b.Y:0.###}  ·  {(item.IsAnchor ? "anchor" : $"{b.Width:0.###} × {b.Height:0.###}")}  ·  source row {editor.Document.Records[item.Record].Line}");
        }
        else _selection.Text = "Drag objects to move them on a 16-unit grid. Drag empty space or use pan mode to move the view.";
    }

    private void Resize(double dw, double dh)
    {
        Board.Cancel(); if (Editor is not { Selected: >= 0 } editor) return;
        var b = editor.Geometry[editor.Selected].Bounds; editor.Resize(b.Width + dw, b.Height + dh);
    }
    public void HandleKey(Silk.NET.Input.Key key)
    {
        if (_playtest is not null) { _playtest.HandleKey(key, true); return; }
        if (_busy || FocusManager.GetFocusedElement() is TextBox) return;
        switch (key)
        {
            case Silk.NET.Input.Key.Escape: Board.SetTool(null); Refresh(); break;
            case Silk.NET.Input.Key.Delete: case Silk.NET.Input.Key.Backspace:
                Board.Cancel(); try { Editor?.DeleteSelected(); } catch (FormatException e) { _status.Text = e.Message; } break;
            case Silk.NET.Input.Key.Left: Board.Pan(-64, 0); break;
            case Silk.NET.Input.Key.Right: Board.Pan(64, 0); break;
            case Silk.NET.Input.Key.Up: Board.Pan(0, -64); break;
            case Silk.NET.Input.Key.Down: Board.Pan(0, 64); break;
        }
    }
    private void RequestOpen()
    {
        _newRequested = false;
        Board.Cancel();
        if (HasUnsavedChanges && !_replaceRequested)
        {
            _replaceRequested = true; Refresh();
            _status.Text = "Save a copy to keep your edits, or press Discard edits & open to replace this document."; return;
        }
        _replaceRequested = false; _ = OpenAsync();
    }
    private void RequestNew()
    {
        Board.Cancel(); _replaceRequested = false;
        if (HasUnsavedChanges && !_newRequested)
        { _newRequested = true; Refresh(); _status.Text = "Save a copy to keep your edits, or press Discard edits & new for a blank LVLX canvas."; return; }
        LoadDocument(SmbxSourceDocument.CreateLvlx());
    }
    private void Busy(bool value) { _busy = value; Refresh(); }
    private static bool Post(DispatcherQueue? dispatcher, Action action)
    { if (dispatcher is null || dispatcher.HasThreadAccess) { action(); return true; } return dispatcher.TryEnqueue(() => action()); }
    private async Task OpenAsync()
    {
        if (_busy) return; Busy(true); var dispatcher = DispatcherQueue.GetForCurrentThread();
        try
        {
            var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".lvl"); picker.FileTypeFilter.Add(".lvlx"); picker.FileTypeFilter.Add(".zip");
            var file = await picker.PickSingleFileAsync(); if (file is null) return;
            using var stream = File.OpenRead(file.Path);
            bool package = Path.GetExtension(file.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase);
            if (stream.Length > (package ? AssetBundle.MaximumArchiveBytes : SmbxSourceDocument.MaximumBytes))
                throw new FormatException("SMBX source files support 8 MiB; ZIP packages support 16 MiB.");
            var bytes = new byte[(int)stream.Length]; await stream.ReadExactlyAsync(bytes);
            if (package)
            {
                var next = new SmbxPackageWorkshop(AssetBundle.FromZip(bytes));
                Post(dispatcher, () => ShowPackage(next)); return;
            }
            var document = Path.GetExtension(file.Name).Equals(".lvlx", StringComparison.OrdinalIgnoreCase)
                ? SmbxSourceDocument.ReadLvlx(bytes, file.Name) : SmbxSourceDocument.ReadLegacy(bytes, file.Name);
            Post(dispatcher, () => LoadDocument(document));
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
        { Post(dispatcher, () => _status.Text = e.Message); }
        finally { Post(dispatcher, () => Busy(false)); }
    }
    private async Task SaveAsync()
    {
        if (_busy || Editor is not { } editor) return;
        Board.Cancel(); Busy(true); var dispatcher = DispatcherQueue.GetForCurrentThread(); int revision = editor.Revision;
        try
        {
            byte[] bytes = editor.Document.WriteOriginal();
            string extension = editor.Document.IsLegacy ? ".lvl" : ".lvlx";
            var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(editor.Document.FileName) + "-edited" + extension };
            picker.FileTypeChoices.Add("SMBX level source", new[] { extension });
            var file = await picker.PickSaveFileAsync(); if (file is null) return;
            await file.WriteBytesAsync(bytes);
            Post(dispatcher, () =>
            {
                _savedRevision = revision;
                if (Package?.Current is { } draft && ReferenceEquals(draft.Editor, editor)) draft.MarkSaved(revision);
                _replaceRequested = _newRequested = false;
                _status.Text = $"Saved {file.Name} in its original source format. Package artwork stays in the original ZIP.";
            });
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
        { Post(dispatcher, () => _status.Text = e.Message); }
        finally { Post(dispatcher, () => Busy(false)); }
    }

    private sealed class PaletteButton(SmbxSourceBoard board, SmbxGeometryKind kind, Func<int> id, Action<string> notice) : Button
    {
        public override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            try { board.BeginPlacement(kind, id(), e); }
            catch (FormatException error) { notice(error.Message); }
            e.Handled = true;
        }
        public override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key is Silk.NET.Input.Key.Enter or Silk.NET.Input.Key.Space)
            {
                try { board.SetTool(kind, id()); } catch (FormatException error) { notice(error.Message); }
                e.Handled = true;
            }
            else base.OnKeyDown(e);
        }
    }
}
