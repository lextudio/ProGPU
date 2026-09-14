using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using ProGPU.Fonts.Inter;
using ProGPU.Vector;
using ProGPU.Samples.Suntrail.Game.Import;

namespace ProGPU.Samples.Suntrail.Presentation;

public sealed partial class SmbxWorkshop
{
    private StackPanel? _warpProperties;
    private ComboBox _warpType = null!, _warpIn = null!, _warpOut = null!;
    private TextBox _warpFile = null!, _warpId = null!;
    private CheckBox _warpEntranceOnly = null!, _warpExitOnly = null!, _warpTwoWay = null!;
    private SmbxSourceDocument? _warpDocument;
    private int _warpSelection = -2;
    private StackPanel CreateWarpControls(Func<string, Action, bool, Button> createButton)
    {
        _warpProperties = new() { Spacing = 6, Visibility = Visibility.Collapsed };
        _warpProperties.AddChild(Label("PIPE / DOOR", 12));
        ComboBox Choice(string label, params string[] values)
        {
            _warpProperties.AddChild(Label(label, 12));
            var box = new ComboBox { Width = 110, MinHeight = 38, Font = InterFontFamily.Regular, FontSize = 12,
                Foreground = new ThemeResourceBrush("SuntrailCream"), Background = new ThemeResourceBrush("SuntrailButton") };
            foreach (string value in values) box.Items.Add(new ComboBoxItem(value) { Font = InterFontFamily.Regular, Foreground = new ThemeResourceBrush("SuntrailCream") });
            AutomationProperties.SetName(box, label); _warpProperties.AddChild(box); return box;
        }
        TextBox Entry(string label)
        {
            _warpProperties.AddChild(Label(label, 12));
            var entry = new TextBox { MinHeight = 38, Font = InterFontFamily.Regular, FontSize = 12 };
            AutomationProperties.SetName(entry, label); _warpProperties.AddChild(entry); return entry;
        }
        CheckBox Flag(string label)
        {
            var flag = new CheckBox { Content = label, Font = InterFontFamily.Regular, FontSize = 12, Foreground = new ThemeResourceBrush("SuntrailCream") };
            AutomationProperties.SetName(flag, label); _warpProperties.AddChild(flag); return flag;
        }
        _warpType = Choice("Warp type", "Instant", "Pipe", "Door", "Portal");
        _warpIn = Choice("Enter direction", "Up", "Left", "Down", "Right");
        _warpOut = Choice("Exit direction", "Down", "Right", "Up", "Left");
        _warpFile = Entry("Target level file"); _warpId = Entry("Target warp (0=start)");
        _warpEntranceOnly = Flag("Level entrance"); _warpExitOnly = Flag("Level exit"); _warpTwoWay = Flag("Two-way");
        var apply = createButton("Apply warp", () =>
        {
            if (_busy || Editor is not { } editor) return;
            try
            {
                if (!int.TryParse(_warpId.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int target)) throw new FormatException("Target warp must be a nonnegative integer.");
                Board.Cancel();
                editor.SetWarpSettings(new(_warpType.SelectedIndex, _warpIn.SelectedIndex + 1, _warpOut.SelectedIndex + 1,
                    _warpFile.Text ?? "", target, _warpEntranceOnly.IsChecked == true, _warpExitOnly.IsChecked == true, _warpTwoWay.IsChecked == true));
                _status.Text = "Warp properties saved in the source. Target files are not opened or executed by this edit.";
            }
            catch (Exception error) when (error is FormatException or ArgumentOutOfRangeException) { _status.Text = error.Message; }
        }, false);
        _warpProperties.AddChild(apply); return _warpProperties;
    }
    private void RefreshWarpProperties()
    {
        if (_warpProperties is null) return;
        var editor = Editor; int selected = editor?.Selected ?? -1;
        bool visible = selected >= 0 && editor!.Geometry[selected].Kind is SmbxGeometryKind.WarpEntrance or SmbxGeometryKind.WarpExit;
        _warpProperties.Visibility = visible ? Visibility.Visible : Visibility.Collapsed; _warpProperties.IsEnabled = !_busy;
        if (ReferenceEquals(_warpDocument, editor?.Document) && _warpSelection == selected) return;
        _warpDocument = editor?.Document; _warpSelection = selected;
        if (!visible) return;
        try
        {
            var settings = editor!.GetWarpSettings();
            _warpType.SelectedIndex = settings.Kind; _warpIn.SelectedIndex = settings.EntranceDirection - 1; _warpOut.SelectedIndex = settings.ExitDirection - 1;
            _warpFile.Text = settings.TargetFile; _warpId.Text = settings.TargetWarp.ToString(CultureInfo.InvariantCulture);
            _warpEntranceOnly.IsChecked = settings.EntranceOnly; _warpExitOnly.IsChecked = settings.ExitOnly; _warpTwoWay.IsChecked = settings.TwoWay;
        }
        catch (FormatException error) { _status.Text = error.Message; }
    }
}
