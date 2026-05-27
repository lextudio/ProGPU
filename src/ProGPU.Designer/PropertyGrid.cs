namespace ProGPU.Designer;

using System;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
using ProGPU.Vector;
using ProGPU.Layout;
using Thickness = Microsoft.UI.Xaml.Thickness;
using HorizontalAlignment = ProGPU.Layout.HorizontalAlignment;
using VerticalAlignment = ProGPU.Layout.VerticalAlignment;
using System.Numerics;

public class PropertyItem
{
    private string _value = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                try
                {
                    OnChanged?.Invoke(_value);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PropertyGrid] Error setting {Name}: {ex.Message}");
                }
            }
        }
    }
    public Action<string>? OnChanged { get; set; }

    public PropertyItem(string name, string value, Action<string> onChanged)
    {
        Name = name;
        _value = value;
        OnChanged = onChanged;
    }
}

public class PropertyGrid : Border
{
    private FrameworkElement? _selectedElement;
    private readonly DataGrid _dataGrid;
    private readonly ProGPU.Text.TtfFont? _font;
    private readonly RichTextBlock _titleText;

    public event Action? PropertyChanged;

    public FrameworkElement? SelectedElement
    {
        get => _selectedElement;
        set
        {
            if (_selectedElement != value)
            {
                _selectedElement = value;
                RefreshProperties();
            }
        }
    }

    public PropertyGrid(ProGPU.Text.TtfFont? font)
    {
        _font = font;
        Background = new ThemeResourceBrush("CardBackground");
        BorderBrush = new ThemeResourceBrush("ControlBorder");
        BorderThickness = new Thickness(1f);
        CornerRadius = 8f;
        Padding = new Thickness(8);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(GridLength.Auto);
        mainGrid.RowDefinitions.Add(GridLength.Star(1f));

        _titleText = new RichTextBlock
        {
            Font = font,
            FontSize = 14f,
            Margin = new Thickness(4, 4, 4, 12),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _titleText.Inlines.Add(new Bold(new Run("Properties")));
        Grid.SetRow(_titleText, 0);
        mainGrid.AddChild(_titleText);

        _dataGrid = new DataGrid
        {
            Font = font,
            FontSize = 11f,
            RowHeight = 26f,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _dataGrid.Columns.Add(new DataGridColumn("Property", "110", "Name"));
        _dataGrid.Columns.Add(new DataGridColumn("Value", "*", "Value"));

        Grid.SetRow(_dataGrid, 1);
        mainGrid.AddChild(_dataGrid);

        Child = mainGrid;

        RefreshProperties();
    }

    public void RefreshProperties()
    {
        _dataGrid.ClearItems();

        if (_selectedElement == null)
        {
            _titleText.Inlines.Clear();
            _titleText.Inlines.Add(new Bold(new Run("Properties (No Selection)")));
            return;
        }

        string typeName = _selectedElement.GetType().Name;
        _titleText.Inlines.Clear();
        _titleText.Inlines.Add(new Bold(new Run($"Properties: {typeName}")));

        // Name
        _dataGrid.AddItem(new PropertyItem("Name", _selectedElement.Name ?? "", val =>
        {
            _selectedElement.Name = val;
            PropertyChanged?.Invoke();
        }));

        // Canvas.Left
        float left = Canvas.GetLeft(_selectedElement);
        _dataGrid.AddItem(new PropertyItem("Canvas.Left", left.ToString("F1"), val =>
        {
            if (float.TryParse(val, out float l))
            {
                Canvas.SetLeft(_selectedElement, l);
                _selectedElement.InvalidateArrange();
                PropertyChanged?.Invoke();
            }
        }));

        // Canvas.Top
        float top = Canvas.GetTop(_selectedElement);
        _dataGrid.AddItem(new PropertyItem("Canvas.Top", top.ToString("F1"), val =>
        {
            if (float.TryParse(val, out float t))
            {
                Canvas.SetTop(_selectedElement, t);
                _selectedElement.InvalidateArrange();
                PropertyChanged?.Invoke();
            }
        }));

        // Width
        float w = float.IsNaN(_selectedElement.Width) ? _selectedElement.Size.X : _selectedElement.Width;
        _dataGrid.AddItem(new PropertyItem("Width", w.ToString("F1"), val =>
        {
            if (float.TryParse(val, out float widthVal))
            {
                _selectedElement.Width = widthVal;
                _selectedElement.InvalidateMeasure();
                _selectedElement.InvalidateArrange();
                PropertyChanged?.Invoke();
            }
        }));

        // Height
        float h = float.IsNaN(_selectedElement.Height) ? _selectedElement.Size.Y : _selectedElement.Height;
        _dataGrid.AddItem(new PropertyItem("Height", h.ToString("F1"), val =>
        {
            if (float.TryParse(val, out float heightVal))
            {
                _selectedElement.Height = heightVal;
                _selectedElement.InvalidateMeasure();
                _selectedElement.InvalidateArrange();
                PropertyChanged?.Invoke();
            }
        }));

        // Opacity
        _dataGrid.AddItem(new PropertyItem("Opacity", _selectedElement.Opacity.ToString("F2"), val =>
        {
            if (float.TryParse(val, out float op))
            {
                _selectedElement.Opacity = Math.Clamp(op, 0f, 1f);
                _selectedElement.Invalidate();
                PropertyChanged?.Invoke();
            }
        }));

        var type = _selectedElement.GetType();

        // CornerRadius
        var crProp = type.GetProperty("CornerRadius");
        if (crProp != null)
        {
            float crVal = crProp.GetValue(_selectedElement) is float f ? f : 0f;
            _dataGrid.AddItem(new PropertyItem("CornerRadius", crVal.ToString("F1"), val =>
            {
                if (float.TryParse(val, out float fcr))
                {
                    crProp.SetValue(_selectedElement, fcr);
                    _selectedElement.Invalidate();
                    PropertyChanged?.Invoke();
                }
            }));
        }

        // Text
        var textProp = type.GetProperty("Text");
        if (textProp != null && textProp.PropertyType == typeof(string))
        {
            string txtVal = textProp.GetValue(_selectedElement) as string ?? "";
            _dataGrid.AddItem(new PropertyItem("Text", txtVal, val =>
            {
                textProp.SetValue(_selectedElement, val);
                _selectedElement.InvalidateMeasure();
                _selectedElement.Invalidate();
                PropertyChanged?.Invoke();
            }));
        }

        // Content (for strings/buttons)
        var contentProp = type.GetProperty("Content");
        if (contentProp != null)
        {
            var contentVal = contentProp.GetValue(_selectedElement);
            string contentStr = "";
            if (contentVal is string s) contentStr = s;
            else if (contentVal is RichTextBlock rtb)
            {
                var sb = new StringBuilder();
                foreach (var inline in rtb.Inlines)
                {
                    if (inline is Run r) sb.Append(r.Text);
                }
                contentStr = sb.ToString();
            }

            _dataGrid.AddItem(new PropertyItem("Content", contentStr, val =>
            {
                if (contentVal is RichTextBlock richText)
                {
                    richText.Inlines.Clear();
                    richText.Inlines.Add(new Run(val));
                    richText.Invalidate();
                }
                else
                {
                    contentProp.SetValue(_selectedElement, val);
                }
                _selectedElement.InvalidateMeasure();
                _selectedElement.InvalidateArrange();
                PropertyChanged?.Invoke();
            }));
        }

        // Minimum
        var minProp = type.GetProperty("Minimum");
        if (minProp != null && minProp.PropertyType == typeof(float))
        {
            float minVal = (float)minProp.GetValue(_selectedElement);
            _dataGrid.AddItem(new PropertyItem("Minimum", minVal.ToString("F1"), val =>
            {
                if (float.TryParse(val, out float fval))
                {
                    minProp.SetValue(_selectedElement, fval);
                    _selectedElement.Invalidate();
                    PropertyChanged?.Invoke();
                }
            }));
        }

        // Maximum
        var maxProp = type.GetProperty("Maximum");
        if (maxProp != null && maxProp.PropertyType == typeof(float))
        {
            float maxVal = (float)maxProp.GetValue(_selectedElement);
            _dataGrid.AddItem(new PropertyItem("Maximum", maxVal.ToString("F1"), val =>
            {
                if (float.TryParse(val, out float fval))
                {
                    maxProp.SetValue(_selectedElement, fval);
                    _selectedElement.Invalidate();
                    PropertyChanged?.Invoke();
                }
            }));
        }

        // Value
        var valProp = type.GetProperty("Value");
        if (valProp != null && valProp.PropertyType == typeof(float))
        {
            float valVal = (float)valProp.GetValue(_selectedElement);
            _dataGrid.AddItem(new PropertyItem("Value", valVal.ToString("F1"), val =>
            {
                if (float.TryParse(val, out float fval))
                {
                    valProp.SetValue(_selectedElement, fval);
                    _selectedElement.Invalidate();
                    PropertyChanged?.Invoke();
                }
            }));
        }

        // IsChecked
        var isCheckedProp = type.GetProperty("IsChecked");
        if (isCheckedProp != null && isCheckedProp.PropertyType == typeof(bool))
        {
            bool isCheckedVal = (bool)isCheckedProp.GetValue(_selectedElement);
            _dataGrid.AddItem(new PropertyItem("IsChecked", isCheckedVal.ToString(), val =>
            {
                if (bool.TryParse(val, out bool bval))
                {
                    isCheckedProp.SetValue(_selectedElement, bval);
                    _selectedElement.Invalidate();
                    PropertyChanged?.Invoke();
                }
            }));
        }

        // IsOn
        var isOnProp = type.GetProperty("IsOn");
        if (isOnProp != null && isOnProp.PropertyType == typeof(bool))
        {
            bool isOnVal = (bool)isOnProp.GetValue(_selectedElement);
            _dataGrid.AddItem(new PropertyItem("IsOn", isOnVal.ToString(), val =>
            {
                if (bool.TryParse(val, out bool bval))
                {
                    isOnProp.SetValue(_selectedElement, bval);
                    _selectedElement.Invalidate();
                    PropertyChanged?.Invoke();
                }
            }));
        }

        // Orientation
        var orientProp = type.GetProperty("Orientation");
        if (orientProp != null)
        {
            var orientVal = orientProp.GetValue(_selectedElement);
            _dataGrid.AddItem(new PropertyItem("Orientation", orientVal.ToString(), val =>
            {
                if (Enum.TryParse(orientProp.PropertyType, val, out var eval))
                {
                    orientProp.SetValue(_selectedElement, eval);
                    _selectedElement.InvalidateMeasure();
                    PropertyChanged?.Invoke();
                }
            }));
        }
    }
}
