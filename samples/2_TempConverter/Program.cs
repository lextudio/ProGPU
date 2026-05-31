using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
using ProGPU.Vector;
namespace TempConverterSample;

public class Program
{
    public static void Main(string[] args)
    {
        AppBuilder<App>.Configure()
            .WithTitle("7GUI - 2. Temperature Converter (WinUI Application)")
            .WithSize(500, 250)
            .Build()
            .Run(args);
    }
}

public class App : Application
{
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new Window();
        window.Title = "7GUI - 2. Temperature Converter";
        window.Width = 500;
        window.Height = 250;

        var rootGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var card = new Border
        {
            Background = ThemeManager.GetBrush("CardBackground"),
            BorderBrush = ThemeManager.GetBrush("ControlBorder"),
            BorderThickness = new Thickness(1f),
            CornerRadius = 8f,
            Padding = new Thickness(24),
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Celsius Input
        var celsiusBox = new TextBox
        {
            Text = "0",
            FontSize = 14f,
            Width = 100f,
            Height = 32f,
            CornerRadius = 4f,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var celsiusLabel = new TextBlock
        {
            Text = "°Celsius",
            FontSize = 14f,
            Foreground = ThemeManager.GetBrush("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 24, 0)
        };

        // Equality text
        var equalsLabel = new TextBlock
        {
            Text = "=",
            FontSize = 18f,
            Foreground = ThemeManager.GetBrush("TextSecondary"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 24, 0)
        };

        // Fahrenheit Input
        var fahrenheitBox = new TextBox
        {
            Text = "32",
            FontSize = 14f,
            Width = 100f,
            Height = 32f,
            CornerRadius = 4f,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var fahrenheitLabel = new TextBlock
        {
            Text = "°Fahrenheit",
            FontSize = 14f,
            Foreground = ThemeManager.GetBrush("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center
        };

        stack.AddChild(celsiusBox);
        stack.AddChild(celsiusLabel);
        stack.AddChild(equalsLabel);
        stack.AddChild(fahrenheitBox);
        stack.AddChild(fahrenheitLabel);
        card.Child = stack;
        rootGrid.AddChild(card);

        window.Content = rootGrid;
        window.Activate();

        // -- REACTIVE BIDIRECTIONAL CONVERSION --
        bool isUpdating = false;

        var invalidBorderPen = new SolidColorBrush(new Vector4(0.9f, 0.15f, 0.15f, 1f));
        var defaultBorderPen = new ThemeResourceBrush("ControlBorder");

        // Celsius Event Stream
        Observable.FromEventPattern(h => celsiusBox.TextChanged += h, h => celsiusBox.TextChanged -= h)
            .Subscribe(_ =>
            {
                if (isUpdating) return;

                string txt = celsiusBox.Text.Trim();
                if (string.IsNullOrEmpty(txt))
                {
                    celsiusBox.BorderBrush = defaultBorderPen;
                    try
                    {
                        isUpdating = true;
                        fahrenheitBox.Text = string.Empty;
                        fahrenheitBox.BorderBrush = defaultBorderPen;
                    }
                    finally
                    {
                        isUpdating = false;
                    }
                    
                    return;
                }

                if (double.TryParse(txt, out double celsius))
                {
                    celsiusBox.BorderBrush = defaultBorderPen;
                    double fahrenheit = celsius * 9.0 / 5.0 + 32.0;

                    try
                    {
                        isUpdating = true;
                        fahrenheitBox.Text = fahrenheit.ToString("G");
                        fahrenheitBox.BorderBrush = defaultBorderPen;
                    }
                    finally
                    {
                        isUpdating = false;
                    }
                }
                else
                {
                    celsiusBox.BorderBrush = invalidBorderPen;
                }
            });

        // Fahrenheit Event Stream
        Observable.FromEventPattern(h => fahrenheitBox.TextChanged += h, h => fahrenheitBox.TextChanged -= h)
            .Subscribe(_ =>
            {
                if (isUpdating) return;

                string txt = fahrenheitBox.Text.Trim();
                if (string.IsNullOrEmpty(txt))
                {
                    fahrenheitBox.BorderBrush = defaultBorderPen;
                    try
                    {
                        isUpdating = true;
                        celsiusBox.Text = string.Empty;
                        celsiusBox.BorderBrush = defaultBorderPen;
                    }
                    finally
                    {
                        isUpdating = false;
                    }
                    
                    return;
                }

                if (double.TryParse(txt, out double fahrenheit))
                {
                    fahrenheitBox.BorderBrush = defaultBorderPen;
                    double celsius = (fahrenheit - 32.0) * 5.0 / 9.0;

                    try
                    {
                        isUpdating = true;
                        celsiusBox.Text = celsius.ToString("G");
                        celsiusBox.BorderBrush = defaultBorderPen;
                    }
                    finally
                    {
                        isUpdating = false;
                    }
                }
                else
                {
                    fahrenheitBox.BorderBrush = invalidBorderPen;
                }
            });
    }
}
