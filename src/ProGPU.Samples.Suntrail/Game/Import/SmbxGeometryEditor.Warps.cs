using System.Globalization;

namespace ProGPU.Samples.Suntrail.Game.Import;

public sealed record SmbxWarpSettings(int Kind, int EntranceDirection, int ExitDirection, string TargetFile, int TargetWarp,
    bool EntranceOnly, bool ExitOnly, bool TwoWay);

public sealed partial class SmbxGeometryEditor
{
    public SmbxWarpSettings GetWarpSettings()
    {
        var row = Document.Records[SelectedWarpRecord()];
        return Read(row);
        static SmbxWarpSettings Read(SmbxSourceRecord row)
        {
            int Number(string name, int fallback) => row.TryGet(name, out var value) ? value.GetInteger() : fallback;
            bool Flag(string name) => row.TryGet(name, out var value) && value.GetBoolean();
            return new(Number("DT", 1), Number("ID", 3), Number("OD", 3), row.TryGet("LF", out var file) ? file.GetString() : "",
                Number("LI", 0), Flag("ET"), Flag("EX"), Flag("TW"));
        }
    }
    private int SelectedWarpRecord()
    {
        if (Selected < 0 || _geometry[Selected].Kind is not (SmbxGeometryKind.WarpEntrance or SmbxGeometryKind.WarpExit))
            throw new FormatException("Select a pipe or door endpoint.");
        return _geometry[Selected].Record;
    }
    public void SetWarpSettings(SmbxWarpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings); ArgumentNullException.ThrowIfNull(settings.TargetFile);
        if (settings.Kind is < 0 or > 3 || Document.IsLegacy && settings.Kind == 3 ||
            settings.EntranceDirection is < 1 or > 4 || settings.ExitDirection is < 1 or > 4 || settings.TargetWarp < 0 || settings.TargetFile.Length > 1024)
            throw new FormatException("Invalid warp type, directions, target ID or filename for this source format.");
        int record = SelectedWarpRecord(); var row = Document.Records[record]; var changes = new List<SmbxFieldEdit>();
        void Add(string name, string value, bool defaultValue = false)
        {
            if (Document.IsLegacy && !row.TryGet(name, out _))
            { if (defaultValue) return; throw new FormatException($"Legacy version {Document.FormatVersion} has no {name} property slot."); }
            if (row.TryGet(name, out var prior) && prior.RawValue == value) return;
            changes.Add(new(record, name, value));
        }
        string Flag(bool value) => Document.IsLegacy ? value ? "#TRUE#" : "#FALSE#" : value ? "1" : "0";
        Add("DT", settings.Kind.ToString(CultureInfo.InvariantCulture));
        Add("ID", settings.EntranceDirection.ToString(CultureInfo.InvariantCulture)); Add("OD", settings.ExitDirection.ToString(CultureInfo.InvariantCulture));
        Add("LF", Document.IsLegacy ? SmbxSourceDocument.EncodeLegacyString(settings.TargetFile) : SmbxSourceDocument.EncodeString(settings.TargetFile), settings.TargetFile.Length == 0);
        Add("LI", settings.TargetWarp.ToString(CultureInfo.InvariantCulture), settings.TargetWarp == 0);
        Add("ET", Flag(settings.EntranceOnly), !settings.EntranceOnly); Add("EX", Flag(settings.ExitOnly), !settings.ExitOnly); Add("TW", Flag(settings.TwoWay), !settings.TwoWay);
        if (changes.Count == 0) return;
        var fields = changes.ToArray();
        if (changes.All(change => row.TryGet(change.Field, out _))) { Apply(fields); return; }
        var splice = Document.PatchObjectFields(record, fields);
        if (splice.HistoryBytes > HistoryByteLimit) throw new FormatException("This warp record exceeds the undo text budget.");
        var next = Document.WithSplice(splice); var projected = Project(next);
        Remember(new([], [], splice.HistoryBytes, Selected, Selected, splice)); Publish(next, projected);
    }
}
