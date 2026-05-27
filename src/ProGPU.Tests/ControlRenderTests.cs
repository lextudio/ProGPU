using System;
using System.IO;
using System.Numerics;
using ProGPU.Layout;
using ProGPU.Vector;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
using ProGPU.Tests.Headless;
using Xunit;
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace ProGPU.Tests;

public class ControlRenderTests
{
    private static HeadlessWindow SharedWindow
    {
        get
        {
            var window = HeadlessWindow.Shared;
            window.Resize(300, 150);
            return window;
        }
    }

    private void VerifyControlStates<T>(T control, string namePrefix) where T : Control
    {
        var window = SharedWindow;
        window.Content = control;

        // 1. Normal State
        window.Render();
        byte[] normalPixels = window.ReadPixels();
        Assert.NotNull(normalPixels);
        string normalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{namePrefix}_normal.png");
        window.SaveScreenshot(normalPath);
        Assert.True(File.Exists(normalPath));

        // 2. Hover State
        control.OnPointerEntered(new PointerRoutedEventArgs { Position = new Vector2(50f, 10f) });
        window.Render();
        byte[] hoverPixels = window.ReadPixels();
        Assert.NotNull(hoverPixels);
        string hoverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{namePrefix}_hover.png");
        window.SaveScreenshot(hoverPath);
        Assert.True(File.Exists(hoverPath));
        control.OnPointerExited(new PointerRoutedEventArgs { Position = new Vector2(-10f, -10f) });

        // 3. Pressed State
        control.OnPointerPressed(new PointerRoutedEventArgs { Position = new Vector2(50f, 10f), IsLeftButtonPressed = true });
        window.Render();
        byte[] pressedPixels = window.ReadPixels();
        Assert.NotNull(pressedPixels);
        string pressedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{namePrefix}_pressed.png");
        window.SaveScreenshot(pressedPath);
        Assert.True(File.Exists(pressedPath));
        control.OnPointerReleased(new PointerRoutedEventArgs { Position = new Vector2(50f, 10f), IsLeftButtonPressed = false });

        // 4. Focus State
        InputSystem.SetFocus(control);
        window.Render();
        byte[] focusPixels = window.ReadPixels();
        Assert.NotNull(focusPixels);
        string focusPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{namePrefix}_focus.png");
        window.SaveScreenshot(focusPath);
        Assert.True(File.Exists(focusPath));
        InputSystem.SetFocus(null);

        // 5. Disabled State
        control.IsEnabled = false;
        window.Render();
        byte[] disabledPixels = window.ReadPixels();
        Assert.NotNull(disabledPixels);
        string disabledPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{namePrefix}_disabled.png");
        window.SaveScreenshot(disabledPath);
        Assert.True(File.Exists(disabledPath));

        // Cleanup shared state
        control.IsEnabled = true;
        window.Content = null;
    }

    [Fact]
    public void Button_AllStates_RenderCorrectly()
    {
        var button = new Button
        {
            Width = 150f,
            Height = 50f,
            Content = new Border { Width = 60f, Height = 20f, Background = new SolidColorBrush(0xFF0000FF) },
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        VerifyControlStates(button, "button");
    }

    [Fact]
    public void Button_AccentStyle_RenderCorrectly()
    {
        var button = new Button
        {
            Width = 150f,
            Height = 50f,
            Content = new Border { Width = 60f, Height = 20f, Background = new SolidColorBrush(0xFF0000FF) },
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var style = ThemeManager.GetResource("AccentButtonStyle") as Style;
        Assert.NotNull(style);
        button.Style = style;
        VerifyControlStates(button, "button_accent");
    }

    [Fact]
    public void Slider_AllStates_RenderCorrectly()
    {
        var slider = new Slider
        {
            Width = 200f,
            Height = 32f,
            Minimum = 0f,
            Maximum = 100f,
            Value = 40f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        VerifyControlStates(slider, "slider");
    }

    [Fact]
    public void ToggleSwitch_AllStates_RenderCorrectly()
    {
        var toggle = new ToggleSwitch
        {
            Width = 150f,
            Height = 40f,
            IsOn = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        VerifyControlStates(toggle, "toggle");
    }

    [Fact]
    public void CheckBox_AllStates_RenderCorrectly()
    {
        var checkbox = new CheckBox
        {
            Width = 150f,
            Height = 32f,
            IsChecked = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        VerifyControlStates(checkbox, "checkbox");
    }

    [Fact]
    public void TextBox_AllStates_RenderCorrectly()
    {
        var textbox = new TextBox
        {
            Width = 200f,
            Height = 36f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        VerifyControlStates(textbox, "textbox");
    }

    [Fact]
    public void ComboBox_AllStates_RenderCorrectly()
    {
        var combo = new ComboBox
        {
            Width = 200f,
            Height = 36f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        combo.Items.Add(new ComboBoxItem("Item 1"));
        combo.Items.Add(new ComboBoxItem("Item 2"));
        VerifyControlStates(combo, "combo");
    }

    [Fact]
    public void ComboBox_PointerSelection_WorksCorrectly()
    {
        var window = SharedWindow;
        var combo = new ComboBox
        {
            Width = 200f,
            Height = 32f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var item1 = new ComboBoxItem("Item 1");
        var item2 = new ComboBoxItem("Item 2");
        combo.Items.Add(item1);
        combo.Items.Add(item2);

        window.Content = combo;
        window.Render(); // Measure & Arrange

        // 1. Initially nothing is selected
        Assert.Null(combo.SelectedItem);

        // 2. Open dropdown
        combo.IsDropDownOpen = true;
        Assert.True(combo.IsDropDownOpen);
        Assert.NotNull(combo.DropDownPopup);

        // 3. Simulate pointer entering and pressing on item2
        var pointerEnteredArgs = new PointerRoutedEventArgs { Position = new Vector2(10f, 10f) };
        item2.OnPointerEntered(pointerEnteredArgs);

        var pointerPressedArgs = new PointerRoutedEventArgs { Position = new Vector2(10f, 10f), IsLeftButtonPressed = true };
        item2.OnPointerPressed(pointerPressedArgs);

        // Dropdown should still be open (the bug was that it collapsed immediately on focus lost)
        Assert.True(combo.IsDropDownOpen);

        // 4. Simulate pointer release on item2
        var pointerReleasedArgs = new PointerRoutedEventArgs { Position = new Vector2(10f, 10f), IsLeftButtonPressed = false };
        item2.OnPointerReleased(pointerReleasedArgs);

        // Dropdown should now be closed and item2 selected!
        Assert.False(combo.IsDropDownOpen);
        Assert.Equal(item2, combo.SelectedItem);
        Assert.True(item2.IsSelected);
        Assert.False(item1.IsSelected);

        // Cleanup
        window.Content = null;
    }

    [Fact]
    public void ComboBox_InsideScrollViewer_DropdownStaysOpen()
    {
        var window = SharedWindow;
        var scroll = new ScrollViewer
        {
            Width = 300f,
            Height = 150f,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var combo = new ComboBox
        {
            Width = 200f,
            Height = 32f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        combo.Items.Add(new ComboBoxItem("Item 1"));
        combo.Items.Add(new ComboBoxItem("Item 2"));

        scroll.Content = combo;
        window.Content = scroll;
        window.Render(); // Measure & Arrange

        // 1. Dropdown initially closed
        Assert.False(combo.IsDropDownOpen);

        // 2. Simulate pointer press on ComboBox header inside ScrollViewer
        var pointerPressedArgs = new PointerRoutedEventArgs { Position = new Vector2(10f, 10f), IsLeftButtonPressed = true };
        combo.OnPointerPressed(pointerPressedArgs);

        // 3. Dropdown should open and stay open
        Assert.True(combo.IsDropDownOpen);
        Assert.True(combo.IsFocused);
        Assert.False(scroll.IsFocused); // ScrollViewer should not have stolen focus

        // Cleanup
        window.Content = null;
    }

    [Fact]
    public void ProgressBar_AllStates_RenderCorrectly()
    {
        var progress = new ProgressBar
        {
            Width = 200f,
            Height = 10f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        VerifyControlStates(progress, "progress");
    }

    [Fact]
    public void ScrollViewer_AllStates_RenderCorrectly()
    {
        var scroll = new ScrollViewer
        {
            Width = 150f,
            Height = 150f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        scroll.Content = new Border { Width = 300f, Height = 300f, Background = new SolidColorBrush(0x00FF00FF) };
        VerifyControlStates(scroll, "scroll");
    }
}
