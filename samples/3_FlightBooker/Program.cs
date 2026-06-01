using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Globalization;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
using ProGPU.Vector;
namespace FlightBookerSample;

public class Program
{
    public static void Main(string[] args)
    {
        AppBuilder<App>.Configure()
            .WithTitle("7GUI - 3. Flight Booker (WinUI Application)")
            .WithSize(450, 350)
            .Build()
            .Run(args);
    }
}

public class App : Application
{
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new Window();
        window.Title = "7GUI - 3. Flight Booker";
        window.Width = 450;
        window.Height = 350;

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

        // ComboBox
        var typeCombo = new ComboBox
        {
            Width = 200f,
            Height = 32f,
            CornerRadius = 4f,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var oneWayItem = new ComboBoxItem("one-way flight");
        var returnItem = new ComboBoxItem("return flight");
        typeCombo.Items.Add(oneWayItem);
        typeCombo.Items.Add(returnItem);
        typeCombo.SelectedItem = oneWayItem;

        // Departure Box
        var departureBox = new TextBox
        {
            Text = DateTime.Today.ToString("yyyy-MM-dd"),
            FontSize = 14f,
            Width = 200f,
            Height = 32f,
            CornerRadius = 4f,
            Margin = new Thickness(0, 0, 0, 12),
            PlaceholderText = "yyyy-MM-dd"
        };

        // Return Box
        var returnBox = new TextBox
        {
            Text = DateTime.Today.ToString("yyyy-MM-dd"),
            FontSize = 14f,
            Width = 200f,
            Height = 32f,
            CornerRadius = 4f,
            Margin = new Thickness(0, 0, 0, 20),
            PlaceholderText = "yyyy-MM-dd",
            IsEnabled = false
        };

        // Book Button
        var bookBtn = new Button
        {
            Content = new TextBlock { Text = "Book", FontSize = 14f, Foreground = ThemeManager.GetBrush("TextPrimary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 200f,
            Height = 36f,
            CornerRadius = 6f
        };

        stack.AddChild(typeCombo);
        stack.AddChild(departureBox);
        stack.AddChild(returnBox);
        stack.AddChild(bookBtn);
        card.Child = stack;
        rootGrid.AddChild(card);

        window.Content = rootGrid;
        window.Activate();

        // -- REACTIVE FLIGHT BOOKER LOGIC --
        var comboSubject = new BehaviorSubject<ComboBoxItem?>(oneWayItem);
        var depTextSubject = new BehaviorSubject<string>(departureBox.Text);
        var retTextSubject = new BehaviorSubject<string>(returnBox.Text);

        // Set up subscriptions to populate state
        Observable.FromEventPattern(h => typeCombo.SelectionChanged += h, h => typeCombo.SelectionChanged -= h)
            .Select(_ => typeCombo.SelectedItem)
            .Subscribe(comboSubject);

        Observable.FromEventPattern(h => departureBox.TextChanged += h, h => departureBox.TextChanged -= h)
            .Select(_ => departureBox.Text)
            .Subscribe(depTextSubject);

        Observable.FromEventPattern(h => returnBox.TextChanged += h, h => returnBox.TextChanged -= h)
            .Select(_ => returnBox.Text)
            .Subscribe(retTextSubject);

        var invalidBorderPen = new SolidColorBrush(new Vector4(0.9f, 0.15f, 0.15f, 1f));
        var defaultBorderPen = new ThemeResourceBrush("ControlBorder");

        // Reactively combine states
        Observable.CombineLatest(comboSubject, depTextSubject, retTextSubject, 
            (item, depStr, retStr) => new { Item = item, Dep = depStr, Ret = retStr })
            .Subscribe(state =>
            {
                bool isReturn = state.Item == returnItem;
                returnBox.IsEnabled = isReturn;

                bool depValid = DateTime.TryParseExact(state.Dep.Trim(), "yyyy-MM-dd", 
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var depDate);
                bool retValid = DateTime.TryParseExact(state.Ret.Trim(), "yyyy-MM-dd", 
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var retDate);

                // Date consistency: return date cannot be before departure date
                bool datesConsistent = !isReturn || (depValid && retValid && depDate <= retDate);

                // Update text box highlight states: both are invalid if they are inconsistent
                departureBox.BorderBrush = (depValid && datesConsistent) ? defaultBorderPen : invalidBorderPen;
                if (isReturn)
                {
                    returnBox.BorderBrush = (retValid && datesConsistent) ? defaultBorderPen : invalidBorderPen;
                }
                else
                {
                    returnBox.BorderBrush = defaultBorderPen; // Reset when disabled
                }

                // Overall flight validity
                bool canBook = false;
                if (!isReturn && depValid)
                {
                    canBook = true;
                }
                else if (isReturn && depValid && retValid && depDate <= retDate)
                {
                    canBook = true;
                }

                bookBtn.IsEnabled = canBook;
            });

        // Click stream
        Observable.FromEventPattern(h => bookBtn.Click += h, h => bookBtn.Click -= h)
            .Subscribe(async _ =>
            {
                string msg;
                if (typeCombo.SelectedItem == returnItem)
                {
                    msg = $"You have successfully booked a return flight:\nDeparture: {departureBox.Text}\nReturn: {returnBox.Text}";
                }
                else
                {
                    msg = $"You have successfully booked a one-way flight:\nDeparture: {departureBox.Text}";
                }

                var dialog = new ContentDialog
                {
                    Title = "Flight Booking Successful",
                    Content = msg,
                    CloseButtonText = "OK"
                };

                await dialog.ShowAsync();
            });
    }
}
