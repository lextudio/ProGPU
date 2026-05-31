using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
namespace CounterSample;

public class Program
{
    public static void Main(string[] args)
    {
        AppBuilder<App>.Configure()
            .WithTitle("7GUI - 1. Counter (WinUI Application)")
            .WithSize(400, 250)
            .Build()
            .Run(args);
    }
}

public class App : Application
{
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new Window();
        window.Title = "7GUI - 1. Counter";
        window.Width = 400;
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

        var textBlock = new TextBlock
        {
            Text = "0",
            FontSize = 24f,
            Foreground = ThemeManager.GetBrush("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
            WidthConstraint = 60f,
            Margin = new Thickness(0, 0, 16, 0)
        };

        var button = new Button
        {
            Content = new TextBlock { Text = "Count", FontSize = 14f, Foreground = ThemeManager.GetBrush("TextPrimary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 100f,
            Height = 36f,
            CornerRadius = 6f
        };

        stack.AddChild(textBlock);
        stack.AddChild(button);
        card.Child = stack;
        rootGrid.AddChild(card);

        window.Content = rootGrid;
        window.Activate();

        // -- REACTIVE STATE ROUTING --
        var counterSubject = new BehaviorSubject<int>(0);

        Observable.FromEventPattern(h => button.Click += h, h => button.Click -= h)
            .Select(_ => counterSubject.Value + 1)
            .Subscribe(counterSubject);

        counterSubject
            .Select(c => c.ToString())
            .Subscribe(txt =>
            {
                textBlock.Text = txt;
            });
    }
}
