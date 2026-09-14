using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Dispatching;
using ProGPU.Fonts.Inter;
using ProGPU.Vector;
using ProGPU.GameEngine.Assets;
using ProGPU.Samples.Suntrail.Game.Import;

namespace ProGPU.Samples.Suntrail.Presentation;

public sealed partial class SmbxWorkshop
{
    public SmbxPackageWorkshop? Package { get; private set; }
    private static readonly AssetBundle EmptyArtworkBundle = AssetBundle.FromFiles([]);
    private SmbxLevelPack? _standaloneArtwork;
    private SmbxLevelPack? CurrentArtwork => Package is not null ? Package.Artwork : _standaloneArtwork;
    private SmbxPackageWorkshop? _listedPackage;
    private ComboBox _packageLevels = null!;
    private StackPanel _packageActions = null!;
    private ScrollViewer _packageScroller = null!;
    private Button _openPackageLevel = null!, _closePackageDraft = null!, _prepareArtwork = null!;
    private Button _exportPackage = null!;
    private bool _replacePackageRequested;

    private ScrollViewer CreatePackageActions(Func<string, Action, bool, Button> createButton)
    {
        _packageActions = new() { Orientation = Orientation.Horizontal, Spacing = 8, Visibility = Visibility.Collapsed };
        _packageLevels = new() { Width = 320, MinHeight = 42, Font = InterFontFamily.Regular, FontSize = 14,
            PlaceholderText = "Choose a level in the ZIP", Foreground = new ThemeResourceBrush("SuntrailCream"), Background = new ThemeResourceBrush("SuntrailButton") };
        AutomationProperties.SetName(_packageLevels, "SMBX package level");
        _packageLevels.SelectionChanged += (_, _) => { _replacePackageRequested = false; RefreshPackageActions(); };
        _packageActions.AddChild(_packageLevels);
        _openPackageLevel = createButton("Open selected level", () =>
        {
            if (_busy || _listedPackage is null || _packageLevels.SelectedItem is not ComboBoxItem { Tag: string path }) return;
            if (!ReferenceEquals(Package, _listedPackage) && HasUnsavedChanges && !_replacePackageRequested)
            {
                _replacePackageRequested = true; RefreshPackageActions();
                _status.Text = "Save your open drafts, or press Discard drafts & open to replace them with this package."; return;
            }
            try { OpenPackageLevel(path); } catch (FormatException error) { _status.Text = error.Message; }
        }, false);
        _closePackageDraft = createButton("Close saved draft", () =>
        {
            if (_busy || Package is null) return;
            try
            {
                Package.CloseCurrent(); AttachEditor(null);
                _status.Text = "Draft closed. Reopening uses the last exported episode, or the input ZIP if none was exported. Standalone source copies are separate.";
            }
            catch (FormatException error) { _status.Text = error.Message; }
        }, false);
        _prepareArtwork = createButton("Refresh artwork", () =>
        {
            if (_busy || Package is null) return;
            try { Board.Cancel(); RefreshCurrentArtwork(); ReportArtwork(); }
            catch (FormatException error) { _status.Text = error.Message; }
        }, false);
        _exportPackage = createButton("Export episode ZIP…", () => _ = ExportPackageAsync(), false);
        foreach (var button in new[] { _openPackageLevel, _closePackageDraft, _prepareArtwork, _exportPackage })
        { button.MinHeight = 42; button.Padding = new Thickness(10, 8, 10, 8); _packageActions.AddChild(button); }
        _packageScroller = new() { Content = _packageActions, Height = 56, Visibility = Visibility.Collapsed,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        return _packageScroller;
    }

    /// <summary>Stages a package picker without replacing the current source draft.</summary>
    public void LoadPackage(AssetBundle bundle) => ShowPackage(new SmbxPackageWorkshop(bundle));
    private void ShowPackage(SmbxPackageWorkshop next)
    {
        _listedPackage = next; _replacePackageRequested = false;
        _packageLevels.Items.Clear();
        foreach (string path in next.Paths)
            _packageLevels.Items.Add(new ComboBoxItem(path) { Tag = path, Font = InterFontFamily.Regular,
                Foreground = new ThemeResourceBrush("SuntrailCream") });
        _packageLevels.SelectedItem = next.Paths.Length == 1 ? _packageLevels.Items[0] : null;
        _packageActions.Visibility = _packageScroller.Visibility = Visibility.Visible; RefreshPackageActions();
        _status.Text = $"{next.Paths.Length} levels found. Choose a level and open it; your current draft remains available.";
    }

    public void OpenPackageLevel(string path)
    {
        if (_listedPackage is not { } next) throw new FormatException("Open a ZIP package first.");
        var draft = next.Open(path, Definitions); // Complete source/art preparation before changing the visible editor.
        Package = next; _replacePackageRequested = false; AttachEditor(draft.Editor); ReportArtwork();
    }

    private void ReportArtwork()
    {
        if (CurrentArtwork is not { } art) return;
        string layout = Board.PreparedArtwork is { } board ? $"{board.Count} placed sprites, {board.Issues.Length} layout issues. " : "";
        if (Board.PreparedArtwork is { } prepared && !prepared.Issues.IsEmpty) layout += prepared.Issues[0].Message + " ";
        _status.Text = $"Source and {art.DecodedBytes / 1024} KiB of artwork prepared. " + layout +
            (art.Issues.IsEmpty ? "Prepared sprites appear on the map. Select an object to inspect its art; compatible game preview remains in development." :
                $"{art.Issues.Length} artwork issues. First: {art.Issues[0].Kind} {art.Issues[0].Id}: {art.Issues[0].Message}");
    }

    private void ClearPackagePicker()
    {
        _listedPackage = null; _replacePackageRequested = false;
        _packageLevels.Items.Clear(); _packageActions.Visibility = _packageScroller.Visibility = Visibility.Collapsed;
    }

    private void RefreshArtworkPreview()
    {
        Board.SetArtwork(CurrentArtwork);
        SmbxArtwork? artwork = null; bool right = false;
        if (Editor is { Selected: >= 0 } editor && CurrentArtwork is { } pack)
        {
            var item = editor.Geometry[editor.Selected];
            SmbxArtworkKind? kind = item.Kind switch
            {
                SmbxGeometryKind.Block => SmbxArtworkKind.Block,
                SmbxGeometryKind.BackgroundAnchor => SmbxArtworkKind.Background,
                SmbxGeometryKind.NpcAnchor => SmbxArtworkKind.Npc, _ => null
            };
            if (kind is { } value && int.TryParse(item.SourceId, out int id)) pack.TryGetArtwork(value, id, out artwork);
            if (item.Kind == SmbxGeometryKind.NpcAnchor && editor.Document.Records[item.Record].TryGet("D", out var direction))
            { try { right = direction.GetInteger() > 0; } catch (FormatException) { } }
        }
        _artworkPreview.SetArtwork(artwork, right);
        if (_artworkCaption is not null) _artworkCaption.Text = _artworkPreview.Description;
    }

    private void RefreshCurrentArtwork()
    {
        if (Package is not null) Package.RefreshArtwork(Definitions);
        else if (Editor is { } editor) _standaloneArtwork = SmbxLevelPack.Prepare(EmptyArtworkBundle, editor.Document, Definitions);
        RefreshArtworkPreview();
    }

    private void RefreshPackageActions()
    {
        if (_packageActions is null) return;
        _packageLevels.IsEnabled = !_busy;
        _openPackageLevel.IsEnabled = !_busy && _listedPackage is not null && _packageLevels.SelectedItem is ComboBoxItem;
        _closePackageDraft.IsEnabled = _prepareArtwork.IsEnabled = !_busy && Package?.Current is not null;
        _exportPackage.IsEnabled = !_busy && Package is not null;
        ButtonText(_openPackageLevel, _replacePackageRequested ? "Discard drafts & open" : "Open selected level");
    }

    private async Task ExportPackageAsync()
    {
        if (_busy || Package is not { } package) return;
        Board.Cancel(); Busy(true); var dispatcher = DispatcherQueue.GetForCurrentThread();
        try
        {
            var snapshot = package.PrepareExport();
            var picker = new FileSavePicker { SuggestedFileName = "suntrail-episode-edited.zip" };
            picker.FileTypeChoices.Add("SMBX episode package", new[] { ".zip" });
            var file = await picker.PickSaveFileAsync(); if (file is null) return;
            await file.WriteBytesAsync(snapshot.CopyBytes());
            Post(dispatcher, () =>
            {
                snapshot.MarkSaved(package);
                _status.Text = $"Saved {file.Name}: edited maps, artwork and original package files. Standalone source copies remain separate.";
                _replaceRequested = _newRequested = _replacePackageRequested = false;
            });
        }
        catch (Exception error) when (error is FormatException or IOException or UnauthorizedAccessException)
        { Post(dispatcher, () => _status.Text = error.Message); }
        finally { Post(dispatcher, () => Busy(false)); }
    }
}
