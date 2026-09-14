using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using ProGPU.Fonts.Inter;
using ProGPU.Samples.Suntrail.Game.Import;

namespace ProGPU.Samples.Suntrail.Presentation;

public sealed partial class SmbxWorkshop
{
    private TextBox? _objectLayer;
    private Button? _assignLayer, _faceLeft, _faceRandom, _faceRight;
    private SmbxSourceDocument? _propertyDocument;
    private int _propertySelection = -2;

    private StackPanel CreatePropertyControls(Func<string, Action, bool, Button> createButton)
    {
        var panel = new StackPanel { Spacing = 6 }; panel.AddChild(Label("OBJECT LAYER", 12));
        _objectLayer = new() { MinHeight = 40, Font = InterFontFamily.Regular, FontSize = 12 };
        AutomationProperties.SetName(_objectLayer, "Selected object's layer name"); panel.AddChild(_objectLayer);
        Button Action(string label, Action apply)
        {
            var button = createButton(label, () =>
            {
                if (_busy) return;
                try { Board.Cancel(); apply(); Refresh(); }
                catch (Exception error) when (error is FormatException or ArgumentOutOfRangeException) { _status.Text = error.Message; }
            }, false);
            panel.AddChild(button); _editing.Add(button); return button;
        }
        _assignLayer = Action("Assign layer", () => Editor?.SetLayer(_objectLayer.Text ?? ""));
        panel.AddChild(Label("NPC DIRECTION", 12));
        _faceLeft = Action("Face left", () => Editor?.SetNpcDirection(-1));
        _faceRandom = Action("Random", () => Editor?.SetNpcDirection(0));
        _faceRight = Action("Face right", () => Editor?.SetNpcDirection(1));
        return panel;
    }

    private void RefreshProperties()
    {
        if (_objectLayer is null) return;
        var editor = Editor; int selected = editor?.Selected ?? -1;
        var item = selected >= 0 ? editor!.Geometry[selected] : default;
        bool layer = selected >= 0 && item.Kind is not (SmbxGeometryKind.Section or SmbxGeometryKind.PlayerAnchor);
        bool npc = selected >= 0 && item.Kind == SmbxGeometryKind.NpcAnchor;
        _objectLayer.IsEnabled = _assignLayer!.IsEnabled = !_busy && layer;
        _faceLeft!.IsEnabled = _faceRandom!.IsEnabled = _faceRight!.IsEnabled = !_busy && npc;
        if (ReferenceEquals(_propertyDocument, editor?.Document) && _propertySelection == selected) return;
        _propertyDocument = editor?.Document; _propertySelection = selected;
        try { _objectLayer.Text = layer && editor!.Document.Records[item.Record].TryGet("LR", out var field) ? field.GetString() : ""; }
        catch (FormatException error) { _objectLayer.Text = ""; _status.Text = error.Message; }
    }
}
