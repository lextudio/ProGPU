using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using ProGPU.Fonts.Inter;
using ProGPU.Samples.Suntrail.Game.Import;
using ProGPU.Vector;
using Key = Silk.NET.Input.Key;

namespace ProGPU.Samples.Suntrail.Presentation;

/// <summary>Shared desktop/mobile/browser playtest shell. The source editor stays intact underneath.</summary>
public sealed class SmbxPlaytestView : Grid
{
    public SmbxSourceBoard Surface { get; } = new();
    private readonly SmbxPlaytest _session;
    private readonly TouchStick _stick = new();
    private readonly HashSet<Key> _keys = new(32);
    private readonly HashSet<Key> _commandKeys = new(4);
    private readonly List<TouchHoldButton> _held = new(5);
    private readonly StackPanel _arrows, _abilities, _settings;
    private readonly Grid _touch;
    private readonly TextBlock _status;
    private readonly Button _pause, _layout, _sprint, _size;
    private bool _touchLeft, _touchRight, _touchJump, _touchRun, _clearing, _settingsOpen, _resumeAfterSettings;
    private int _options;
    private (int Section, int Falls, int Transfers, bool Paused) _lastStatus = (-1, -1, -1, false);
    public event Action? CloseRequested;
    public event Action<int>? TouchOptionsChanged;

    public SmbxPlaytestView(SmbxPlaytest session, SmbxLevelPack? artwork, int touchOptions,
        Func<string, Action, bool, Button> createButton)
    {
        _session = session; Name = "SmbxGeometryPlaytest";
        Background = new ThemeResourceBrush("SuntrailInk");
        Surface.SetPlaytest(session, artwork); AddChild(Surface);
        var header = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(12) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        Button Action(string text, Action action)
        {
            var button = createButton(text, action, false); button.Padding = new Thickness(12, 8, 12, 8);
            button.MinHeight = 42; actions.AddChild(button); return button;
        }
        Action("← Edit", Close);
        _pause = Action("Pause", () => { _session.SetPaused(!_session.Paused); ClearInput(); RefreshStatus(); });
        Action("Restart", () => { _session.Restart(); ClearInput(); RefreshStatus(); });
        Action("Controls", ToggleSettings);
        header.AddChild(new ScrollViewer { Content = actions, Height = 50, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        header.AddChild(Label("GEOMETRY PLAYTEST · Suntrail movement · all blocks solid · NPCs and events inactive", 12));
        _status = Label("", 12); header.AddChild(_status); AddChild(header);

        _touch = new Grid { VerticalAlignment = VerticalAlignment.Bottom, Height = 160, Margin = new Thickness(16, 0, 16, 12) };
        _arrows = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };
        _arrows.AddChild(Hold("←", value => _touchLeft = value)); _arrows.AddChild(Hold("→", value => _touchRight = value));
        _touch.AddChild(_arrows);
        _stick.HorizontalAlignment = HorizontalAlignment.Left; _stick.Width = 280; _stick.Height = 160;
        AutomationProperties.SetName(_stick, "Playtest movement thumbstick");
        _stick.InputChanged += SyncInput; _touch.AddChild(_stick);
        _abilities = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };
        _abilities.AddChild(Hold("RUN", value => _touchRun = value));
        _abilities.AddChild(Hold("JUMP", value =>
        {
            if (value && !_touchJump) Surface.PlaytestInput = Surface.PlaytestInput with { JumpPressed = true };
            _touchJump = value;
        }));
        _abilities.AddChild(Hold("USE", value =>
        { if (value) Surface.PlaytestInput = Surface.PlaytestInput with { UsePressed = true }; }));
        _touch.AddChild(_abilities); AddChild(_touch);

        _settings = new StackPanel { Spacing = 12, Margin = new Thickness(20), Padding = new Thickness(20),
            Background = new ThemeResourceBrush("SuntrailInk"), MaxWidth = 620,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
        _settings.AddChild(Label("PLAYTEST CONTROLS", 22));
        _settings.AddChild(Label("Hold JUMP to leap higher. USE enters a local pipe or door.\nKeyboard: arrows / A D · SPACE jump · SHIFT run · S use · R restart", 13));
        _layout = createButton("", () => ApplyOptions((_options & ~3) | (((_options & 3) + 1) % 3)), false);
        _sprint = createButton("", () => ApplyOptions(_options ^ 4), false);
        _size = createButton("", () => ApplyOptions(_options ^ 8), false);
        _settings.AddChild(_layout); _settings.AddChild(_sprint); _settings.AddChild(_size);
        var report = new System.Text.StringBuilder("Playtest policy: 30 × 48 Suntrail courier; all BLOCK rectangles collide.\nNPCs, zones, events, layer rules and other object behaviors are not simulated. Warp aperture thickness defaults to 32 units; pipe/door entry uses USE without an animation.\n");
        foreach (var issue in session.Issues)
            report.Append("Line ").Append(session.Snapshot.Document.Records[issue.Record].Line).Append(": ").Append(issue.Message).Append('\n');
        if (session.IssueCount > session.Issues.Length) report.Append("Additional unsupported records: ").Append(session.IssueCount - session.Issues.Length);
        _settings.AddChild(new ScrollViewer { Content = Label(report.ToString(), 12), MaxHeight = 110,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        _settings.AddChild(createButton("Back to playtest", ToggleSettings, true)); AddChild(_settings);
        ApplyOptions(touchOptions, false); Surface.PlaytestUpdated += RefreshStatus; RefreshStatus();
    }
    private static TextBlock Label(string text, float size) => new()
    { Text = text, Font = InterFontFamily.Regular, FontSize = size, Foreground = new ThemeResourceBrush("SuntrailCream"), IsHitTestVisible = false };
    private TouchHoldButton Hold(string text, Action<bool> changed)
    {
        var button = new TouchHoldButton(value => { changed(value); SyncInput(); })
        {
            Content = text, Font = InterFontFamily.Bold, FontSize = 16, Opacity = .72f,
            Background = new ThemeResourceBrush("SuntrailButton"), Foreground = new ThemeResourceBrush("SuntrailCream"),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        AutomationProperties.SetName(button, "Playtest " + text); _held.Add(button); return button;
    }
    private static void Text(Button button, string text)
    { if (button.Content is TextBlock label) label.Text = text; else button.Content = text; }
    private void ApplyOptions(int value, bool save = true)
    {
        ClearInput(); if (value < 0 || value > 14 || (value & 3) == 3) value = 12; _options = value;
        var layout = (TouchLayout)(value & 3); bool large = (value & 8) != 0, auto = (value & 4) != 0;
        _stick.Floating = layout == TouchLayout.FloatingStick; _stick.AutoSprint = auto;
        _stick.Visibility = layout == TouchLayout.Buttons ? Visibility.Collapsed : Visibility.Visible;
        _arrows.Visibility = layout == TouchLayout.Buttons ? Visibility.Visible : Visibility.Collapsed;
        ((FrameworkElement)_abilities.Children[0]).Visibility = auto ? Visibility.Collapsed : Visibility.Visible;
        foreach (var button in _held)
        {
            bool jump = Equals(button.Content, "JUMP");
            button.Width = button.MinWidth = button.Height = jump ? large ? 94 : 78 : large ? 74 : 62;
            button.CornerRadius = new(jump ? button.Height / 2 : 22);
        }
        Text(_layout, "Layout: " + (layout == TouchLayout.Buttons ? "Arrow buttons" : layout == TouchLayout.FixedStick ? "Fixed stick" : "Floating stick"));
        Text(_sprint, auto ? "Sprint: Automatic / outer stick edge" : "Sprint: Separate RUN button");
        Text(_size, large ? "Buttons: Large" : "Buttons: Standard");
        if (save) TouchOptionsChanged?.Invoke(value);
    }
    private void ToggleSettings()
    {
        ClearInput();
        if (!_settingsOpen) { _resumeAfterSettings = !_session.Paused; _session.SetPaused(true); }
        else if (_resumeAfterSettings) _session.SetPaused(false);
        _settingsOpen = !_settingsOpen; _settings.Visibility = _settingsOpen ? Visibility.Visible : Visibility.Collapsed;
        RefreshStatus();
    }
    private void SyncInput()
    {
        if (_clearing) return;
        if (_session.Paused) { Surface.PlaytestInput = default; return; }
        bool left = _keys.Contains(Key.Left) || _keys.Contains(Key.A), right = _keys.Contains(Key.Right) || _keys.Contains(Key.D);
        Surface.PlaytestInput = new(Math.Clamp((right || _touchRight ? 1 : 0) - (left || _touchLeft ? 1 : 0) + _stick.Axis, -1, 1),
            JumpHeld() || _touchJump, Surface.PlaytestInput.JumpPressed,
            _touchRun || _keys.Contains(Key.ShiftLeft) || _keys.Contains(Key.ShiftRight) || _stick.Sprint ||
            ((_options & 4) != 0 && (_touchLeft || _touchRight)), Surface.PlaytestInput.UsePressed);
    }
    private bool JumpHeld() => _keys.Contains(Key.Space) || _keys.Contains(Key.Up) || _keys.Contains(Key.W);
    public void HandleKey(Key key, bool down)
    {
        if (!down) { _keys.Remove(key); _commandKeys.Remove(key); SyncInput(); return; }
        bool wasJumping = JumpHeld(); if (_keys.Contains(key)) return;
        if (key is Key.Escape or Key.P)
        {
            if (!_commandKeys.Add(key)) return;
            if (_settingsOpen) ToggleSettings(); else { _session.SetPaused(!_session.Paused); ClearInput(); RefreshStatus(); }
            return;
        }
        if (_settingsOpen) return;
        if (key == Key.R) { if (!_commandKeys.Add(key)) return; _session.Restart(); ClearInput(); RefreshStatus(); return; }
        if (key is not (Key.Left or Key.Right or Key.A or Key.D or Key.Space or Key.Up or Key.W or Key.Down or Key.S or Key.ShiftLeft or Key.ShiftRight)) return;
        if (_session.Paused) return;
        _keys.Add(key);
        if (!wasJumping && key is Key.Space or Key.Up or Key.W)
            Surface.PlaytestInput = Surface.PlaytestInput with { JumpPressed = true };
        if (key is Key.Down or Key.S) Surface.PlaytestInput = Surface.PlaytestInput with { UsePressed = true };
        SyncInput();
    }
    public void ClearInput()
    {
        _clearing = true; _keys.Clear(); foreach (var button in _held) button.Reset(); _stick.Reset();
        _touchLeft = _touchRight = _touchJump = _touchRun = false; Surface.PlaytestInput = default;
        _session.ClearPendingInput(); _clearing = false;
    }
    public void Deactivate()
    { _resumeAfterSettings = false; _commandKeys.Clear(); _session.SetPaused(true); ClearInput(); RefreshStatus(); }
    public void Close()
    {
        Deactivate(); Surface.ClearPlaytest(); Surface.SetEditor(null);
        Surface.PlaytestUpdated -= RefreshStatus; CloseRequested?.Invoke();
    }
    private void RefreshStatus()
    {
        var state = (_session.SectionIndex, _session.Falls, _session.Transfers, _session.Paused);
        if (_lastStatus == state) return; _lastStatus = state;
        _status.Text = $"Section {state.SectionIndex + 1}/{_session.Sections.Length} · falls {state.Falls} · warps {state.Transfers}" +
            (_session.Paused ? " · paused" : "") + $" · {_session.IssueCount} reported limitations";
        Text(_pause, _session.Paused ? "Resume" : "Pause");
        _touch.Visibility = _session.Paused ? Visibility.Collapsed : Visibility.Visible;
    }
    protected override void ArrangeOverride(ProGPU.Scene.Rect rect)
    {
        _stick.Width = Math.Min(280, rect.Width * .43f);
        base.ArrangeOverride(rect);
    }
}
