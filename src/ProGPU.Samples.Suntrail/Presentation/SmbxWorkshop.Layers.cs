using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using ProGPU.Fonts.Inter;
using ProGPU.Vector;
using ProGPU.Samples.Suntrail.Game.Import;

namespace ProGPU.Samples.Suntrail.Presentation;

public sealed partial class SmbxWorkshop
{
    private ComboBox? _layerPicker;
    private Button? _layerVisibility;
    private TextBox? _layerName;
    private SmbxEditorLayers? _listedLayers;
    private StackPanel CreateLayerControls(Func<string, Action, bool, Button> createButton)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.AddChild(Label("VIEW LAYERS", 12));
        _layerPicker = new() { Width = 110, MinHeight = 40, Font = InterFontFamily.Regular, FontSize = 12,
            Foreground = new ThemeResourceBrush("SuntrailCream"), Background = new ThemeResourceBrush("SuntrailButton") };
        AutomationProperties.SetName(_layerPicker, "Source layer to show or hide");
        _layerPicker.SelectionChanged += (_, _) => RefreshLayerButton(); panel.AddChild(_layerPicker);
        _layerVisibility = createButton("Hide layer", () =>
        {
            if (_busy || Board.Layers is not { } layers || _layerPicker.SelectedItem is not ComboBoxItem { Tag: int index }) return;
            Board.SetLayerVisible(index, !layers.Layers[index].Visible); Refresh();
            _status.Text = "Editor visibility changed. Saved layer flags and object data are unchanged.";
        }, false);
        panel.AddChild(_layerVisibility); _editing.Add(_layerVisibility);
        var showAll = createButton("Show all layers", () => { if (_busy) return; Board.ShowAllLayers(); Refresh(); }, false);
        panel.AddChild(showAll); _editing.Add(showAll);
        _layerName = new() { MinHeight = 40, Font = InterFontFamily.Regular, FontSize = 12 };
        AutomationProperties.SetName(_layerName, "New layer name"); panel.AddChild(_layerName);
        Button Change(string label, Action change)
        {
            var button = createButton(label, () =>
            {
                if (_busy) return;
                try { Board.Cancel(); change(); Refresh(); }
                catch (FormatException error) { _status.Text = error.Message; }
            }, false);
            panel.AddChild(button); _editing.Add(button); return button;
        }
        Change("Create layer", () => Editor?.CreateLayer(_layerName.Text ?? ""));
        Change("Rename layer", () =>
        {
            if (Board.Layers is not { } layers || _layerPicker.SelectedItem is not ComboBoxItem { Tag: int index } || layers.Layers[index].Name is not { } previous)
                throw new FormatException("Select a named layer to rename.");
            Editor?.RenameLayer(previous, _layerName.Text ?? "");
            _status.Text = "Layer references updated. Script text and unsupported custom event actions are unchanged.";
        });
        return panel;
    }
    private void RefreshLayers()
    {
        if (_layerPicker is null) return;
        var model = Board.Layers;
        if (!ReferenceEquals(_listedLayers, model))
        {
            int old = _layerPicker.SelectedItem is ComboBoxItem { Tag: int value } ? value : -1;
            string? name = old >= 0 && _listedLayers is { } prior ? prior.Layers[old].Name : null;
            _listedLayers = model; _layerPicker.Items.Clear();
            int selected = 0;
            if (model is not null)
                for (int i = 0; i < model.Layers.Length; i++)
                {
                    var layer = model.Layers[i];
                    _layerPicker.Items.Add(new ComboBoxItem($"{layer.Label} ({layer.ObjectCount})") { Tag = i, Font = InterFontFamily.Regular,
                        Foreground = new ThemeResourceBrush("SuntrailCream") });
                    if (old >= 0 && layer.Name == name) selected = i;
                }
            _layerPicker.SelectedItem = _layerPicker.Items.Count == 0 ? null : _layerPicker.Items[selected];
        }
        _layerPicker.IsEnabled = !_busy && model is not null;
        if (_layerName is not null) _layerName.IsEnabled = !_busy && model is not null;
        RefreshLayerButton();
    }
    private void RefreshLayerButton()
    {
        if (_layerVisibility is null) return;
        bool valid = Board.Layers is { } layers && _layerPicker?.SelectedItem is ComboBoxItem { Tag: int index } &&
            (uint)index < (uint)layers.Layers.Length;
        _layerVisibility.IsEnabled = !_busy && valid;
        if (valid && _layerPicker!.SelectedItem is ComboBoxItem { Tag: int selected })
            ButtonText(_layerVisibility, Board.Layers!.Layers[selected].Visible ? "Hide layer" : "Show layer");
    }
}
