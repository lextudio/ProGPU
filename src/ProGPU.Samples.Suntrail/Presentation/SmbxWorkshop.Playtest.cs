using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProGPU.Samples.Suntrail.Game.Import;

namespace ProGPU.Samples.Suntrail.Presentation;

public sealed partial class SmbxWorkshop
{
    private SmbxPlaytestView? _playtest;
    public Func<int>? TouchOptionsProvider { get; set; }
    public event Action<int>? TouchOptionsChanged;
    private void StartPlaytest(Func<string, Action, bool, Button> createButton)
    {
        if (_busy || Editor is not { } editor) return;
        Board.Cancel();
        int player = editor.Selected >= 0 && editor.Geometry[editor.Selected].Kind == SmbxGeometryKind.PlayerAnchor
            ? editor.Geometry[editor.Selected].Record : -1;
        var session = new SmbxPlaytest(editor.Document, player);
        // Reprepare current IDs only once at start, using the current source draft.
        RefreshCurrentArtwork();
        var view = new SmbxPlaytestView(session, CurrentArtwork, TouchOptionsProvider?.Invoke() ?? 12, createButton);
        view.TouchOptionsChanged += value => TouchOptionsChanged?.Invoke(value);
        view.CloseRequested += () => { RemoveChild(view); _playtest = null; Refresh(); };
        SetRowSpan(view, 3); AddChild(view); _playtest = view;
    }
    public void HandleKeyUp(Silk.NET.Input.Key key) => _playtest?.HandleKey(key, false);
    public void Deactivate() => _playtest?.Deactivate();
}
