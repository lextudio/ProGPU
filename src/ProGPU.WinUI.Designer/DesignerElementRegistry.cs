namespace ProGPU.WinUI.Designer;

using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using ProGPU.Scene;
using ProGPU.Text;

public static class DesignerElementRegistry
{
    private static readonly Dictionary<string, Func<FrameworkElement>> FactoriesByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Type, Func<FrameworkElement>> FactoriesByType = new();

    static DesignerElementRegistry()
    {
        Register<Button>("Button", static () => new Button());
        Register<TextBox>("TextBox", static () => new TextBox());
        Register<TextBlock>("TextBlock", static () => new TextBlock());
        Register<ComboBox>("ComboBox", static () => new ComboBox());
        Register<Slider>("Slider", static () => new Slider());
        Register<CheckBox>("CheckBox", static () => new CheckBox());
        Register<RadioButton>("RadioButton", static () => new RadioButton());
        Register<ProgressBar>("ProgressBar", static () => new ProgressBar());
        Register<ProgressRing>("ProgressRing", static () => new ProgressRing());
        Register<RatingControl>("RatingControl", static () => new RatingControl());
        Register<ToggleSwitch>("ToggleSwitch", static () => new ToggleSwitch());
        Register<CalendarView>("CalendarView", static () => new CalendarView());
        Register<DatePicker>("DatePicker", static () => new DatePicker());
        Register<PasswordBox>("PasswordBox", static () => new PasswordBox());
        Register<TreeView>("TreeView", static () => new TreeView());
        Register<DataGrid>("DataGrid", static () => new DataGrid());
        Register<ColorPicker>("ColorPicker", static () => new ColorPicker());
        Register<StackPanel>("StackPanel", static () => new StackPanel());
        Register<Grid>("Grid", static () => new Grid());
        Register<Canvas>("Canvas", static () => new Canvas());
        Register<Border>("Border", static () => new Border());
        Register<ScrollViewer>("ScrollViewer", static () => new ScrollViewer());
        Register<SplitView>("SplitView", static () => new SplitView());
        Register<WrapPanel>("WrapPanel", static () => new WrapPanel());
        Register<DockPanel>("DockPanel", static () => new DockPanel());
        Register<GridSplitter>("GridSplitter", static () => new GridSplitter());
    }

    public static void Register<TElement>(string name, Func<TElement> factory)
        where TElement : FrameworkElement
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);

        FrameworkElement Create() => factory();
        FactoriesByName[name] = Create;
        FactoriesByType[typeof(TElement)] = Create;
    }

    public static bool TryCreate(string name, TtfFont? font, out FrameworkElement element)
    {
        element = null!;
        if (!FactoriesByName.TryGetValue(name, out var factory))
        {
            return false;
        }

        element = factory();
        ApplyDesignerDefaults(element, name, font);
        return true;
    }

    public static bool TryCreateLike(FrameworkElement source, out FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(source);

        element = null!;
        if (!FactoriesByType.TryGetValue(source.GetType(), out var factory))
        {
            return false;
        }

        element = factory();
        return true;
    }

    public static void ApplyDesignerDefaults(FrameworkElement element, string name, TtfFont? font)
    {
        ArgumentNullException.ThrowIfNull(element);

        element.IsHitTestVisible = false;
        if (float.IsNaN(element.Width) || element.Width <= 0)
        {
            element.Width = 120f;
        }

        if (float.IsNaN(element.Height) || element.Height <= 0)
        {
            element.Height = 36f;
        }

        if (element is Button button)
        {
            button.Content = CreateLabel(name, font);
        }
        else if (element is TextBlock textBlock)
        {
            textBlock.Text = name;
        }
        else if (element is CheckBox checkBox)
        {
            checkBox.Content = CreateLabel(name, font);
        }
        else if (element is RadioButton radioButton)
        {
            radioButton.Content = CreateLabel(name, font);
        }
        else if (element is ToggleSwitch toggleSwitch)
        {
            toggleSwitch.Content = CreateLabel(name, font);
        }
        else if (element is ComboBox comboBox)
        {
            comboBox.PlaceholderText = name;
        }
        else if (element is TextBox textBox)
        {
            textBox.PlaceholderText = name;
        }
        else if (element is PasswordBox passwordBox)
        {
            passwordBox.PlaceholderText = name;
        }
    }

    public static bool IsDropContainer(FrameworkElement element)
    {
        if (element is Button or CheckBox or RadioButton or ToggleSwitch or ComboBox)
        {
            return false;
        }

        return element is Panel or Border or ContentControl or SplitView;
    }

    public static bool TryAddChild(FrameworkElement target, FrameworkElement child)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(child);

        if (target is Panel panel)
        {
            panel.Children.Add(child);
            return true;
        }

        if (target is Border border)
        {
            border.Child = child;
            return true;
        }

        if (target is SplitView splitView)
        {
            splitView.Content = child;
            return true;
        }

        if (target is ContentControl contentControl &&
            target is not Button and not CheckBox and not RadioButton and not ToggleSwitch and not ComboBox)
        {
            contentControl.Content = child;
            return true;
        }

        if (target is ContainerVisual container)
        {
            container.AddChild(child);
            return true;
        }

        return false;
    }

    public static bool RemoveFromParent(FrameworkElement child)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.Parent is Panel panel)
        {
            return panel.Children.Remove(child);
        }

        if (child.Parent is Border border && border.Child == child)
        {
            border.Child = null;
            return true;
        }

        if (child.Parent is SplitView splitView)
        {
            if (splitView.Content == child)
            {
                splitView.Content = null;
                return true;
            }

            if (splitView.Pane == child)
            {
                splitView.Pane = null;
                return true;
            }
        }

        if (child.Parent is ContentControl contentControl && contentControl.Content == child)
        {
            contentControl.Content = null;
            return true;
        }

        if (child.Parent is ContainerVisual container)
        {
            container.RemoveChild(child);
            return true;
        }

        return false;
    }

    public static IEnumerable<FrameworkElement> GetLogicalChildren(FrameworkElement parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        if (parent is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is FrameworkElement childElement)
                {
                    yield return childElement;
                }
            }

            yield break;
        }

        if (parent is Border { Child: FrameworkElement borderChild })
        {
            yield return borderChild;
            yield break;
        }

        if (parent is SplitView splitView)
        {
            if (splitView.Pane is FrameworkElement pane)
            {
                yield return pane;
            }

            if (splitView.Content is FrameworkElement content)
            {
                yield return content;
            }

            yield break;
        }

        if (parent is ContentControl contentControl &&
            parent is not Button and not CheckBox and not RadioButton and not ToggleSwitch and not ComboBox &&
            contentControl.Content is FrameworkElement contentChild)
        {
            yield return contentChild;
        }
    }

    public static bool IsLogicalChild(FrameworkElement parent, FrameworkElement child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (parent is Panel panel)
        {
            return panel.Children.Contains(child);
        }

        if (parent is Border border)
        {
            return border.Child == child;
        }

        if (parent is SplitView splitView)
        {
            return splitView.Pane == child || splitView.Content == child;
        }

        return parent is ContentControl contentControl &&
            contentControl.Content == child;
    }

    private static RichTextBlock CreateLabel(string text, TtfFont? font)
    {
        var richText = new RichTextBlock { Font = font ?? PopupService.DefaultFont };
        richText.Inlines.Add(new Run(text));
        return richText;
    }
}
