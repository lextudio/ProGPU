using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
namespace TimerSample;

public class Program
{
    public static void Main(string[] args)
    {
        AppBuilder<App>.Configure()
            .WithTitle("7GUI - 4. Timer (WinUI Application)")
            .WithSize(450, 300)
            .Build()
            .Run(args);
    }
}

public class App : Application
{
    private float _elapsedTime = 0.0f;
    private float _maxDuration = 15.0f;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new Window();
        window.Title = "7GUI - 4. Timer";
        window.Width = 450;
        window.Height = 300;

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
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };

        // 1. Elapsed Label
        var elapsedLabel = new TextBlock
        {
            Text = "Elapsed Time: 0.0s",
            FontSize = 14f,
            Foreground = ThemeManager.GetBrush("TextPrimary"),
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // 2. ProgressBar
        var progressBar = new ProgressBar
        {
            Minimum = 0f,
            Maximum = 100f,
            Value = 0f,
            Width = 300f,
            Height = 6f,
            Margin = new Thickness(0, 0, 0, 16)
        };

        // 3. Slider Label
        var sliderLabel = new TextBlock
        {
            Text = "Duration: 15.0s",
            FontSize = 12f,
            Foreground = ThemeManager.GetBrush("TextSecondary"),
            Margin = new Thickness(0, 0, 0, 4)
        };

        // 4. Slider
        var durationSlider = new Microsoft.UI.Xaml.Controls.Slider
        {
            Minimum = 0f,
            Maximum = 30f,
            Value = 15f,
            Width = 300f,
            Margin = new Thickness(0, 0, 0, 20)
        };

        // 5. Reset Button
        var resetBtn = new Button
        {
            Content = new TextBlock { Text = "Reset Timer", FontSize = 14f, Foreground = ThemeManager.GetBrush("TextPrimary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 120f,
            Height = 36f,
            CornerRadius = 6f,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        stack.AddChild(elapsedLabel);
        stack.AddChild(progressBar);
        stack.AddChild(sliderLabel);
        stack.AddChild(durationSlider);
        stack.AddChild(resetBtn);
        card.Child = stack;
        rootGrid.AddChild(card);

        window.Content = rootGrid;
        window.Activate();

        // Update UI Helper
        void UpdateUI()
        {
            float percentage = _maxDuration > 0f ? (_elapsedTime / _maxDuration) * 100f : 100f;
            progressBar.Value = percentage;
            elapsedLabel.Text = $"Elapsed Time: {_elapsedTime:F1}s";
            sliderLabel.Text = $"Duration: {_maxDuration:F1}s";
        }

        // -- REACTIVE TIMER UPDATE ROUTING --
        // Periodic ticks every 50ms (20fps)
        var timerDisposable = Observable.Interval(TimeSpan.FromMilliseconds(50))
            .Subscribe(_ =>
            {
                UIThread.Post(() =>
                {
                    if (_elapsedTime < _maxDuration)
                    {
                        _elapsedTime = Math.Min(_maxDuration, _elapsedTime + 0.05f);
                        UpdateUI();
                    }
                });
            });

        // Track Slider changes
        Observable.FromEventPattern(h => durationSlider.ValueChanged += h, h => durationSlider.ValueChanged -= h)
            .Subscribe(_ =>
            {
                _maxDuration = durationSlider.Value;
                UpdateUI();
            });

        // Track Reset button clicks
        Observable.FromEventPattern(h => resetBtn.Click += h, h => resetBtn.Click -= h)
            .Subscribe(_ =>
            {
                _elapsedTime = 0.0f;
                UpdateUI();
            });

        // Initialize display
        UpdateUI();
    }
}
