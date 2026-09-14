using System.Text;
using ProGPU.GameEngine.Assets;
using ProGPU.Samples.Suntrail.Game.Import;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class SmbxPackageWorkshopTests
{
    [Fact]
    public void EpisodeExportContainsAllOpenEditsAndKeepsSavedEditsAfterDraftClosure()
    {
        var session = new SmbxPackageWorkshop(Package());
        var first = session.Open("episode/map-0.lvlx"); first.Editor.Add(SmbxGeometryKind.Block, 17, 32, 64);
        byte[] firstBytes = first.Editor.Document.WriteOriginal();
        var second = session.Open("episode/map-1.lvlx"); second.Editor.Add(SmbxGeometryKind.NpcAnchor, 28, 96, 128);
        byte[] secondBytes = second.Editor.Document.WriteOriginal();
        var snapshot = session.PrepareExport(); Assert.True(session.HasUnsavedChanges);
        var exported = AssetBundle.FromZip(snapshot.CopyBytes());
        Assert.Equal(firstBytes, exported.Get("episode/map-0.lvlx").ToArray());
        Assert.Equal(secondBytes, exported.Get("episode/map-1.lvlx").ToArray());
        snapshot.MarkSaved(session); Assert.False(session.HasUnsavedChanges);
        session.CloseCurrent();
        var later = AssetBundle.FromZip(session.PrepareExport().CopyBytes());
        Assert.Equal(secondBytes, later.Get("episode/map-1.lvlx").ToArray());
        Assert.Equal(secondBytes, session.Open("episode/map-1.lvlx").Editor.Document.WriteOriginal());
    }

    [Fact]
    public void ExportSnapshotsExcludePointerPreviewsAndOldCompletionCannotHideNewerEdits()
    {
        var session = new SmbxPackageWorkshop(Package()); var draft = session.Open("episode/map-0.lvlx");
        draft.Editor.Add(SmbxGeometryKind.Block, 17, 32, 64);
        byte[] original = draft.Editor.Document.WriteOriginal();
        draft.Editor.BeginMove(draft.Editor.Selected); draft.Editor.Move(16, 16);
        var old = session.PrepareExport();
        Assert.Equal(original, AssetBundle.FromZip(old.CopyBytes()).Get("episode/map-0.lvlx").ToArray());
        draft.Editor.CommitMove(); byte[] moved = draft.Editor.Document.WriteOriginal();
        old.MarkSaved(session); Assert.True(session.HasUnsavedChanges);
        var latest = session.PrepareExport(); latest.MarkSaved(session); Assert.False(session.HasUnsavedChanges);
        old.MarkSaved(session); Assert.False(session.HasUnsavedChanges);
        session.CloseCurrent(); Assert.Equal(moved, session.Open("episode/map-0.lvlx").Editor.Document.WriteOriginal());
        Assert.Throws<ArgumentException>(() => latest.MarkSaved(new SmbxPackageWorkshop(Package())));
    }

    [Fact]
    public void UncompletedExportDoesNotAdvancePackageBaselineOrSavedRevisions()
    {
        var session = new SmbxPackageWorkshop(Package()); var draft = session.Open("episode/map-0.lvlx");
        byte[] baseline = draft.Editor.Document.WriteOriginal();
        draft.Editor.Add(SmbxGeometryKind.Block, 1, 64, 64);
        var canceled = session.PrepareExport(); byte[] owned = canceled.CopyBytes(); Array.Fill(owned, (byte)0);
        Assert.NotEqual(owned, canceled.CopyBytes()); Assert.True(session.HasUnsavedChanges);
        // A separate source-file save allows closure but must not act as an episode export.
        draft.MarkSaved(draft.Editor.Revision); session.CloseCurrent();
        Assert.Equal(baseline, session.Open("episode/map-0.lvlx").Editor.Document.WriteOriginal());
    }

    internal static AssetBundle Package(int count = 2, bool malformedSecond = false)
    {
        var files = new List<KeyValuePair<string, ReadOnlyMemory<byte>>>();
        for (int i = 0; i < count; i++)
            files.Add(new($"episode/map-{i}.lvlx", malformedSecond && i == 1 ? Encoding.UTF8.GetBytes("broken") : SmbxSourceDocument.CreateLvlx($"Original map {i}").WriteOriginal()));
        return AssetBundle.FromFiles(files);
    }

    [Fact]
    public void SwitchingRetainsExactEditorSourceHistoryAndDirtyStateAcrossMaps()
    {
        var session = new SmbxPackageWorkshop(Package());
        var first = session.Open("episode/map-0.lvlx"); byte[] original = first.Editor.Document.WriteOriginal();
        first.Editor.Add(SmbxGeometryKind.Block, 73, 128, 256);
        byte[] changed = first.Editor.Document.WriteOriginal();
        var second = session.Open("episode/map-1.lvlx");
        Assert.NotSame(first, second); Assert.True(session.HasUnsavedChanges); Assert.False(second.HasUnsavedChanges);
        Assert.Same(first, session.Open("episode/map-0.lvlx")); Assert.Equal(changed, first.Editor.Document.WriteOriginal());
        Assert.Same(first.Editor.Document, session.Artwork!.Document);
        Assert.Single(session.Artwork.Issues.ToArray());
        first.Editor.Undo(); Assert.Equal(original, first.Editor.Document.WriteOriginal());
        first.Editor.Redo(); Assert.Equal(changed, first.Editor.Document.WriteOriginal());
        Assert.Equal(2, session.OpenDraftCount);
    }

    [Fact]
    public void FailedOpenLeavesCurrentSourceAndPreparedArtworkAvailable()
    {
        var session = new SmbxPackageWorkshop(Package(malformedSecond: true));
        var first = session.Open("episode/map-0.lvlx"); var artwork = session.Artwork;
        Assert.Throws<FormatException>(() => session.Open("episode/map-1.lvlx"));
        Assert.Throws<FormatException>(() => session.Open("../outside.lvlx"));
        Assert.Same(first, session.Current); Assert.Same(artwork, session.Artwork); Assert.Equal(1, session.OpenDraftCount);
    }

    [Fact]
    public void SavedCopiesAndDraftClosureDoNotRewriteThePackage()
    {
        var session = new SmbxPackageWorkshop(Package()); var draft = session.Open("episode/map-0.lvlx");
        byte[] original = draft.Editor.Document.WriteOriginal();
        draft.Editor.Add(SmbxGeometryKind.NpcAnchor, 5, 0, 0);
        Assert.Throws<FormatException>(session.CloseCurrent); Assert.Same(draft, session.Current);
        int saved = draft.Editor.Revision; draft.MarkSaved(saved); Assert.False(session.HasUnsavedChanges);
        draft.Editor.Add(SmbxGeometryKind.Block, 6, 96, 96);
        draft.MarkSaved(saved); Assert.True(session.HasUnsavedChanges); // An earlier async save cannot hide newer edits.
        draft.MarkSaved(draft.Editor.Revision); session.CloseCurrent();
        Assert.Null(session.Current); Assert.Null(session.Artwork); Assert.Equal(0, session.OpenDraftCount);
        Assert.Equal(original, session.Open("episode/map-0.lvlx").Editor.Document.WriteOriginal());
    }

    [Fact]
    public void DraftLimitRequiresExplicitClosureAndRefreshReplacesOnlyPreparedArt()
    {
        var session = new SmbxPackageWorkshop(Package(9));
        for (int i = 0; i < 8; i++) session.Open($"episode/map-{i}.lvlx");
        var active = session.Current!; var oldArt = session.Artwork;
        Assert.Throws<FormatException>(() => session.Open("episode/map-8.lvlx")); Assert.Same(active, session.Current);
        active.Editor.Add(SmbxGeometryKind.Block, 912, 0, 0);
        session.RefreshArtwork(); Assert.NotSame(oldArt, session.Artwork); Assert.Same(active, session.Current);
        Assert.Equal(912, Assert.Single(session.Artwork!.Issues.ToArray()).Id);
        active.MarkSaved(active.Editor.Revision); session.CloseCurrent(); session.Open("episode/map-8.lvlx");
        Assert.Equal(8, session.OpenDraftCount);
    }
}
