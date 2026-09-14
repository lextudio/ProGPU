namespace ProGPU.Samples.Suntrail.Game.Import;

/// <summary>Editor-only visibility of source LR groups. Does not execute events or change saved HD/LC fields.</summary>
public sealed class SmbxEditorLayers
{
    public sealed class Layer
    {
        public string? Name { get; }
        public int ObjectCount { get; internal set; }
        public bool Visible { get; internal set; } = true;
        public string Label => Name is null ? "Unassigned objects" : Name.Length == 0 ? "(empty layer name)" : Name;
        internal Layer(string? name) => Name = name;
    }
    private readonly Layer[] _layers;
    private readonly int[] _membership;
    private readonly SmbxGeometryIssue[] _issues;
    public ReadOnlySpan<Layer> Layers => _layers;
    public ReadOnlySpan<SmbxGeometryIssue> Issues => _issues;
    public bool IsVisible(int geometryIndex) => _layers[_membership[geometryIndex]].Visible;

    /// <summary>O(R + G + L) preparation; O(1) indexed visibility checks. Names compare ordinally and empty names differ from missing LR.</summary>
    public SmbxEditorLayers(SmbxGeometryEditor editor, SmbxEditorLayers? previous = null)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var layers = new List<Layer> { new(null) }; var names = new Dictionary<string, int>(StringComparer.Ordinal);
        var issues = new List<SmbxGeometryIssue>();
        int Group(string name)
        {
            if (names.TryGetValue(name, out int known)) return known;
            int index = layers.Count; names.Add(name, index); layers.Add(new(name)); return index;
        }
        for (int r = 0; r < editor.Document.Records.Length; r++)
        {
            var row = editor.Document.Records[r];
            if (row.Section != "LAYERS" || row.SectionPath != "LAYERS") continue;
            try { Group(row.Get("LR").GetString()); }
            catch (FormatException error) { issues.Add(new(r, error.Message)); }
        }
        _membership = new int[editor.Geometry.Length];
        for (int i = 0; i < _membership.Length; i++)
        {
            var item = editor.Geometry[i]; var row = editor.Document.Records[item.Record]; int group = 0;
            try { if (row.TryGet("LR", out var field)) group = Group(field.GetString()); }
            catch (FormatException error) { issues.Add(new(item.Record, error.Message)); }
            _membership[i] = group; layers[group].ObjectCount++;
        }
        if (previous is not null)
            foreach (var layer in previous._layers)
            {
                if (layer.Name is null) layers[0].Visible = layer.Visible;
                else if (names.TryGetValue(layer.Name, out int index)) layers[index].Visible = layer.Visible;
            }
        _layers = layers.ToArray(); _issues = issues.ToArray();
    }

    public void SetVisible(int layerIndex, bool visible)
    {
        if ((uint)layerIndex >= (uint)_layers.Length) throw new ArgumentOutOfRangeException(nameof(layerIndex));
        _layers[layerIndex].Visible = visible;
    }
    public void ShowAll() { foreach (var layer in _layers) layer.Visible = true; }
}
