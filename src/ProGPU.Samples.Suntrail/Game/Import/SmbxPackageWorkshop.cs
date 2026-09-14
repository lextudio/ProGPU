using ProGPU.GameEngine.Assets;

namespace ProGPU.Samples.Suntrail.Game.Import;

/// <summary>
/// Source drafts share one immutable package. At most eight editors retain their
/// document and bounded delta history; only the active map owns decoded artwork.
/// Opening another map prepares before switching. No file is written by this model.
/// </summary>
public sealed class SmbxPackageWorkshop
{
    public const int MaximumOpenDrafts = 8;
    public sealed class Draft
    {
        public SmbxGeometryEditor Editor { get; }
        public int SavedRevision { get; private set; }
        public bool HasUnsavedChanges => Editor.Revision != SavedRevision;
        internal Draft(SmbxGeometryEditor editor) => Editor = editor;
        public void MarkSaved(int revision)
        {
            if (revision < 0 || revision > Editor.Revision) throw new ArgumentOutOfRangeException(nameof(revision));
            SavedRevision = revision;
        }
    }
    private AssetBundle _bundle;
    private long _exportSequence, _completedExportSequence;
    private readonly string[] _paths;
    private readonly Dictionary<string, Draft> _drafts = new(StringComparer.Ordinal);
    public ReadOnlySpan<string> Paths => _paths;
    public Draft? Current { get; private set; }
    public SmbxLevelPack? Artwork { get; private set; }
    public bool HasUnsavedChanges => _drafts.Values.Any(d => d.HasUnsavedChanges);
    public int OpenDraftCount => _drafts.Count;

    /// <summary>Owned archive and exact draft revisions captured before an asynchronous save.</summary>
    public sealed class ExportSnapshot
    {
        private readonly byte[] _bytes;
        private readonly SmbxPackageWorkshop _owner;
        private readonly (Draft Draft, int Revision)[] _revisions;
        private readonly AssetBundle _content;
        private readonly long _sequence;
        internal ExportSnapshot(SmbxPackageWorkshop owner, byte[] bytes, (Draft, int)[] revisions, AssetBundle content, long sequence)
        { _owner = owner; _bytes = bytes; _revisions = revisions; _content = content; _sequence = sequence; }
        public byte[] CopyBytes() => (byte[])_bytes.Clone();
        public int ByteLength => _bytes.Length;
        public void MarkSaved(SmbxPackageWorkshop owner)
        {
            if (!ReferenceEquals(owner, _owner)) throw new ArgumentException("Export belongs to another package.", nameof(owner));
            if (_sequence <= owner._completedExportSequence) return;
            owner._bundle = _content; owner._completedExportSequence = _sequence;
            foreach (var (draft, revision) in _revisions)
                if (owner._drafts.TryGetValue(draft.Editor.Document.FileName, out var current) && ReferenceEquals(current, draft))
                    draft.MarkSaved(revision);
        }
    }

    public ExportSnapshot PrepareExport()
    {
        var replacements = new List<KeyValuePair<string, ReadOnlyMemory<byte>>>(_drafts.Count);
        var revisions = new List<(Draft, int)>(_drafts.Count);
        foreach (var (path, draft) in _drafts)
        {
            // A pointer preview is not a committed source edit and is excluded.
            replacements.Add(new(path, draft.Editor.Document.WriteOriginal()));
            revisions.Add((draft, draft.Editor.Revision));
        }
        var content = _bundle.WithReplacements(replacements);
        byte[] bytes = content.WriteZip();
        return new(this, bytes, revisions.ToArray(), content, checked(++_exportSequence));
    }

    public SmbxPackageWorkshop(AssetBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle); _bundle = bundle; _paths = SmbxLevelPack.LevelPaths(bundle);
        if (_paths.Length == 0) throw new FormatException("The package contains no LVL or LVLX levels.");
    }

    public Draft Open(string path, SmbxConfigPack? definitions = null)
    {
        path = AssetBundle.ResolvePath("", path);
        if (Array.IndexOf(_paths, path) < 0) throw new FormatException("Select a level listed in this package.");
        bool retained = _drafts.TryGetValue(path, out var draft);
        if (!retained && _drafts.Count == MaximumOpenDrafts)
            throw new FormatException("Eight drafts are open. Save a copy and close a draft before opening another level.");
        var pack = retained ? SmbxLevelPack.Prepare(_bundle, draft!.Editor.Document, definitions) : SmbxLevelPack.Open(_bundle, path, definitions);
        draft ??= new(new SmbxGeometryEditor(pack.Document));
        // Previous source, history and active artwork stay available on failure.
        Current?.Editor.CancelMove();
        if (!retained) _drafts.Add(path, draft);
        Current = draft; Artwork = pack; return draft;
    }

    public void RefreshArtwork(SmbxConfigPack? definitions = null)
    {
        if (Current is not { } draft) return;
        var next = SmbxLevelPack.Prepare(_bundle, draft.Editor.Document, definitions); Artwork = next;
    }

    /// <summary>Close a saved draft. Reopening reads the last successfully exported episode snapshot, or the input ZIP before any export. Standalone source copies do not change that baseline.</summary>
    public void CloseCurrent()
    {
        if (Current is not { } draft) return;
        if (draft.HasUnsavedChanges) throw new FormatException("Save a copy before closing this draft.");
        draft.Editor.CancelMove(); _drafts.Remove(draft.Editor.Document.FileName); Current = null; Artwork = null;
    }
}
