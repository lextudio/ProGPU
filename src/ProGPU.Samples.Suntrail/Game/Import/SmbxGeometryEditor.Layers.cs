namespace ProGPU.Samples.Suntrail.Game.Import;

public sealed partial class SmbxGeometryEditor
{
    public void CreateLayer(string name)
    {
        ValidateLayerName(name);
        foreach (var row in Document.Records)
            if (row.SectionPath == "LAYERS" && row.TryGet("LR", out var field) && field.GetString() == name)
                throw new FormatException("This layer is already declared.");
        var splice = Document.InsertLayer(name);
        if (splice.HistoryBytes > HistoryByteLimit) throw new FormatException("This declaration exceeds the undo budget.");
        var next = Document.WithSplice(splice); var projected = Project(next);
        Remember(new([], [], splice.HistoryBytes, Selected, Selected, splice)); Publish(next, projected);
    }

    public void RenameLayer(string previous, string next)
    {
        ValidateLayerName(previous); ValidateLayerName(next); if (previous == next) return;
        var edits = Document.RenameLayerReferences(previous, next);
        if (edits.Length == 0) throw new FormatException("The selected layer is not declared or referenced.");
        Apply(edits);
    }
    private static void ValidateLayerName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length is 0 or > 1024) throw new FormatException("Declared layer names must contain 1–1024 characters.");
    }
}
