using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProGPU.GameEngine.Assets;
using ProGPU.Samples.Suntrail.Game.Import;

namespace ProGPU.Samples.Suntrail.Presentation;

public sealed partial class SmbxWorkshop
{
    public SmbxConfigPack? Definitions { get; private set; }
    private readonly TextBlock _definitionInfo;
    public void LoadDefinitions(IAssetSource bundle, string mainPath)
    {
        var next = new SmbxConfigPack(bundle, mainPath);
        ReplaceDefinitions(next);
        _status.Text = "Base definitions and artwork loaded. Scripts and object algorithms remain inactive.";
    }
    private async Task OpenDefinitionsAsync()
    {
        if (_busy) return;
        Board.Cancel(); Busy(true); var dispatcher = DispatcherQueue.GetForCurrentThread();
        IndexedAssetArchive? pending = null;
        try
        {
            var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".zip");
            var file = await picker.PickSingleFileAsync(); if (file is null) return;
            using var stream = File.OpenRead(file.Path);
            pending = await IndexedAssetArchive.ReadAsync(stream);
            var candidates = pending.Paths.Where(p => Path.GetFileName(p).Equals("main.ini", StringComparison.OrdinalIgnoreCase) &&
                !p.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (candidates.Length != 1) throw new FormatException("Select a definition ZIP containing exactly one main.ini and its split object files.");
            var next = new SmbxConfigPack(pending, candidates[0], ownsSource: true);
            pending = null;
            if (!Post(dispatcher, () =>
            {
                try
                {
                    ReplaceDefinitions(next);
                    _status.Text = "Base definitions and artwork loaded. Scripts and unsupported object behavior remain inactive.";
                }
                catch (Exception error) when (error is FormatException or IOException or UnauthorizedAccessException)
                { _status.Text = error.Message; }
            })) next.Dispose();
        }
        catch (Exception error) when (error is FormatException or IOException or UnauthorizedAccessException)
        { Post(dispatcher, () => _status.Text = error.Message); }
        finally { pending?.Dispose(); Post(dispatcher, () => Busy(false)); }
    }
    private void ReplaceDefinitions(SmbxConfigPack next)
    {
        // Prepare first; a failure keeps the old definition source and editor state.
        try
        {
            if (Package is not null) Package.RefreshArtwork(next);
            else if (Editor is { } editor) _standaloneArtwork = SmbxLevelPack.Prepare(EmptyArtworkBundle, editor.Document, next);
        }
        catch { next.Dispose(); throw; }
        var previous = Definitions; Definitions = next; previous?.Dispose();
        RefreshDefinitionInfo(); RefreshArtworkPreview();
    }
    private void RefreshDefinitionInfo()
    {
        if (_definitionInfo is null) return;
        if (Definitions is null) { _definitionInfo.Text = "Base definitions: none loaded."; return; }
        _definitionInfo.Text = "Base definitions loaded. Select a block, background object or NPC to inspect metadata.";
        if (Editor is not { Selected: >= 0 } editor) return;
        var item = editor.Geometry[editor.Selected];
        SmbxArtworkKind? kind = item.Kind switch
        {
            SmbxGeometryKind.Block => SmbxArtworkKind.Block, SmbxGeometryKind.BackgroundAnchor => SmbxArtworkKind.Background,
            SmbxGeometryKind.NpcAnchor => SmbxArtworkKind.Npc, _ => null
        };
        if (kind is null || !int.TryParse(item.SourceId, out int id) || id <= 0) return;
        try
        {
            var definition = Definitions.Get(kind.Value, id);
            if (definition is null) { _definitionInfo.Text = $"No base definition supplied for {kind} {id}."; return; }
            string physical = definition.PhysicalWidth is { } width && definition.PhysicalHeight is { } height ? $"{width} × {height}" : "unspecified";
            string graphics = definition.GraphicsWidth is { } gw && definition.GraphicsHeight is { } gh ? $"{gw} × {gh}" : "unspecified";
            _definitionInfo.Text = $"{definition.Name ?? item.SourceId} · physical {physical} · graphic frame {graphics} · metadata only";
        }
        catch (FormatException error) { _definitionInfo.Text = error.Message; }
    }
}
