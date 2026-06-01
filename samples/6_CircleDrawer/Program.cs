using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Documents;
using ProGPU.Scene;
using ProGPU.Vector;

namespace CircleDrawerSample;

public class Circle
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Radius { get; set; } = 15f; // Default diameter = 30
    public bool IsSelected { get; set; }
    public Ellipse? UIElement { get; set; }

    public Circle(float x, float y, float radius = 15f)
    {
        X = x;
        Y = y;
        Radius = radius;
    }
}

// -- COMMAND PATTERN FOR UNDO/REDO --
public interface ICommand
{
    void Execute();
    void Undo();
}

public class AddCircleCommand : ICommand
{
    private readonly CircleCanvas _canvas;
    private readonly Circle _circle;

    public AddCircleCommand(CircleCanvas canvas, Circle circle)
    {
        _canvas = canvas;
        _circle = circle;
    }

    public void Execute()
    {
        _canvas.Circles.Add(_circle);

        var ellipse = new Ellipse
        {
            Width = _circle.Radius * 2f,
            Height = _circle.Radius * 2f,
            Fill = ThemeManager.GetBrush("ControlBackground"),
            Stroke = ThemeManager.GetBrush("ControlBorder"),
            StrokeThickness = 1.5f
        };

        Canvas.SetLeft(ellipse, _circle.X - _circle.Radius);
        Canvas.SetTop(ellipse, _circle.Y - _circle.Radius);

        _circle.UIElement = ellipse;
        _canvas.Children.Add(ellipse);
        _canvas.Invalidate();
    }

    public void Undo()
    {
        _canvas.Circles.Remove(_circle);
        if (_circle.UIElement != null)
        {
            _canvas.Children.Remove(_circle.UIElement);
            _circle.UIElement = null;
        }
        _canvas.Invalidate();
    }
}

public class ResizeCircleCommand : ICommand
{
    private readonly CircleCanvas _canvas;
    private readonly Circle _circle;
    private readonly float _oldRadius;
    private readonly float _newRadius;

    public ResizeCircleCommand(CircleCanvas canvas, Circle circle, float oldRadius, float newRadius)
    {
        _canvas = canvas;
        _circle = circle;
        _oldRadius = oldRadius;
        _newRadius = newRadius;
    }

    public void Execute()
    {
        _circle.Radius = _newRadius;
        if (_circle.UIElement != null)
        {
            _circle.UIElement.Width = _newRadius * 2f;
            _circle.UIElement.Height = _newRadius * 2f;
            Canvas.SetLeft(_circle.UIElement, _circle.X - _newRadius);
            Canvas.SetTop(_circle.UIElement, _circle.Y - _newRadius);
        }
        _canvas.Invalidate();
    }

    public void Undo()
    {
        _circle.Radius = _oldRadius;
        if (_circle.UIElement != null)
        {
            _circle.UIElement.Width = _oldRadius * 2f;
            _circle.UIElement.Height = _oldRadius * 2f;
            Canvas.SetLeft(_circle.UIElement, _circle.X - _oldRadius);
            Canvas.SetTop(_circle.UIElement, _circle.Y - _oldRadius);
        }
        _canvas.Invalidate();
    }
}

public class CircleCanvas : Canvas
{
    public List<Circle> Circles { get; } = new();
    public Circle? SelectedCircle { get; set; }

    public event EventHandler? SelectionChanged;
    public event EventHandler<Circle>? RightClickCircle;

    public float? WidthConstraint { get; set; }
    public float? HeightConstraint { get; set; }

    public CircleCanvas()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    public override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        e.Handled = true;
        InputSystem.SetFocus(this);

        Vector2 localPos = e.Position;

        if (e.IsRightButtonPressed)
        {
            var hit = HitTestCircles(localPos);
            if (hit != null)
            {
                SelectCircle(hit);
                RightClickCircle?.Invoke(this, hit);
            }
        }
        else if (e.IsLeftButtonPressed)
        {
            var hit = HitTestCircles(localPos);
            if (hit != null)
            {
                SelectCircle(hit);
            }
            else
            {
                SelectCircle(null);

                var newCircle = new Circle(localPos.X, localPos.Y);
                App.ExecuteCommand(new AddCircleCommand(this, newCircle));
            }
        }

        Invalidate();
        base.OnPointerPressed(e);
    }

    private Circle? HitTestCircles(Vector2 pos)
    {
        for (int i = Circles.Count - 1; i >= 0; i--)
        {
            var circle = Circles[i];
            float dist = Vector2.Distance(pos, new Vector2(circle.X, circle.Y));
            if (dist <= circle.Radius)
            {
                return circle;
            }
        }
        return null;
    }

    public void SelectCircle(Circle? circle)
    {
        if (SelectedCircle != circle)
        {
            if (SelectedCircle != null)
            {
                SelectedCircle.IsSelected = false;
                if (SelectedCircle.UIElement != null)
                {
                    SelectedCircle.UIElement.Fill = ThemeManager.GetBrush("ControlBackground");
                    SelectedCircle.UIElement.Stroke = ThemeManager.GetBrush("ControlBorder");
                    SelectedCircle.UIElement.StrokeThickness = 1.5f;
                }
            }

            SelectedCircle = circle;

            if (SelectedCircle != null)
            {
                SelectedCircle.IsSelected = true;
                if (SelectedCircle.UIElement != null)
                {
                    SelectedCircle.UIElement.Fill = ThemeManager.GetBrush("SelectionHighlight");
                    SelectedCircle.UIElement.Stroke = ThemeManager.GetBrush("SystemAccentColor");
                    SelectedCircle.UIElement.StrokeThickness = 2.0f;
                }
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    public override void OnRender(DrawingContext context)
    {
        var rect = new Rect(Vector2.Zero, Size);
        context.DrawRoundedRectangle(ThemeManager.GetBrush("CardBackground"), 
            new Pen(ThemeManager.GetBrush("ControlBorder"), 1.2f), rect, 8f);

        base.OnRender(context);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        base.MeasureOverride(availableSize);

        float w = WidthConstraint ?? availableSize.X;
        float h = HeightConstraint ?? availableSize.Y;
        return new Vector2(w, h);
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        AppBuilder<App>.Configure()
            .WithTitle("7GUI - 6. Circle Drawer (WinUI Application)")
            .WithSize(600, 500)
            .Build()
            .Run(args);
    }
}

public class App : Application
{
    private static readonly Stack<ICommand> _undoStack = new();
    private static readonly Stack<ICommand> _redoStack = new();

    private static Button? _undoBtn;
    private static Button? _redoBtn;
    private static CircleCanvas? _canvas;

    public static void ExecuteCommand(ICommand cmd)
    {
        cmd.Execute();
        _undoStack.Push(cmd);
        _redoStack.Clear();
        UpdateUndoRedoButtons();
    }

    private static void UpdateUndoRedoButtons()
    {
        if (_undoBtn != null) _undoBtn.IsEnabled = _undoStack.Count > 0;
        if (_redoBtn != null) _redoBtn.IsEnabled = _redoStack.Count > 0;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new Window();
        window.Title = "7GUI - 6. Circle Drawer";
        window.Width = 600;
        window.Height = 500;

        var rootGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var mainLayout = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(16)
        };
        mainLayout.RowDefinitions.Add(new GridLength(45f, GridUnitType.Absolute)); // Buttons Toolbar Row
        mainLayout.RowDefinitions.Add(new GridLength(1f, GridUnitType.Star));       // Canvas Drawing Area Row

        // --- BUTTONS TOOLBAR ---
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _undoBtn = new Button
        {
            Content = new TextBlock { Text = "Undo", FontSize = 14f, Foreground = ThemeManager.GetBrush("TextPrimary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 90f,
            Height = 32f,
            CornerRadius = 4f,
            Margin = new Thickness(0, 0, 10, 0),
            IsEnabled = false
        };

        _redoBtn = new Button
        {
            Content = new TextBlock { Text = "Redo", FontSize = 14f, Foreground = ThemeManager.GetBrush("TextPrimary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 90f,
            Height = 32f,
            CornerRadius = 4f,
            IsEnabled = false
        };

        toolbar.AddChild(_undoBtn);
        toolbar.AddChild(_redoBtn);
        mainLayout.AddChild(toolbar);
        Grid.SetRow(toolbar, 0);

        // --- DRAWING CANVAS ---
        _canvas = new CircleCanvas
        {
            WidthConstraint = 560f,
            HeightConstraint = 400f,
            Margin = new Thickness(0, 8, 0, 0)
        };
        mainLayout.AddChild(_canvas);
        Grid.SetRow(_canvas, 1);

        rootGrid.AddChild(mainLayout);

        window.Content = rootGrid;
        window.Activate();

        // -- REACTIVE ROUTING AND COMMAND HANDLING --

        // Undo Click
        Observable.FromEventPattern(h => _undoBtn.Click += h, h => _undoBtn.Click -= h)
            .Subscribe(_ =>
            {
                if (_undoStack.Count > 0)
                {
                    PopupService.DismissNonDialogPopups();

                    var cmd = _undoStack.Pop();
                    cmd.Undo();
                    _redoStack.Push(cmd);
                    UpdateUndoRedoButtons();
                    _canvas.SelectCircle(null);
                }
            });

        // Redo Click
        Observable.FromEventPattern(h => _redoBtn.Click += h, h => _redoBtn.Click -= h)
            .Subscribe(_ =>
            {
                if (_redoStack.Count > 0)
                {
                    PopupService.DismissNonDialogPopups();

                    var cmd = _redoStack.Pop();
                    cmd.Execute();
                    _undoStack.Push(cmd);
                    UpdateUndoRedoButtons();
                    _canvas.SelectCircle(null);
                }
            });

        // Right-Click on circle opens popup dialog slider
        _canvas.RightClickCircle += (sender, circle) =>
        {
            float initialRadius = circle.Radius;

            var popupStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Padding = new Thickness(14),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var popupTitle = new TextBlock
            {
                Text = $"Adjust Circle Diameter: {(circle.Radius * 2f):F0}px",
                FontSize = 12f,
                Foreground = ThemeManager.GetBrush("TextPrimary"),
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var diameterSlider = new Microsoft.UI.Xaml.Controls.Slider
            {
                Minimum = 10f,
                Maximum = 150f,
                Value = circle.Radius * 2f,
                Width = 180f,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var okBtn = new Button
            {
                Content = new TextBlock { Text = "Save", FontSize = 12f, Foreground = ThemeManager.GetBrush("TextPrimary"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                Width = 70f,
                Height = 28f,
                CornerRadius = 4f,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            popupStack.AddChild(popupTitle);
            popupStack.AddChild(diameterSlider);
            popupStack.AddChild(okBtn);

            var popupBorder = new Border
            {
                Background = ThemeManager.GetBrush("CardBackground"),
                BorderBrush = ThemeManager.GetBrush("SystemAccentColor"),
                BorderThickness = new Thickness(1.5f),
                CornerRadius = 8f,
                Child = popupStack
            };

            Observable.FromEventPattern(h => diameterSlider.ValueChanged += h, h => diameterSlider.ValueChanged -= h)
                .Subscribe(_ =>
                {
                    circle.Radius = diameterSlider.Value / 2f;
                    if (circle.UIElement != null)
                    {
                        circle.UIElement.Width = circle.Radius * 2f;
                        circle.UIElement.Height = circle.Radius * 2f;
                        Canvas.SetLeft(circle.UIElement, circle.X - circle.Radius);
                        Canvas.SetTop(circle.UIElement, circle.Y - circle.Radius);
                    }
                    popupTitle.Text = $"Adjust Circle Diameter: {diameterSlider.Value:F0}px";
                    _canvas.Invalidate();
                });

            Vector2 absPos = _canvas.Offset;
            var current = _canvas.Parent;
            while (current != null)
            {
                absPos += current.Offset;
                current = current.Parent;
            }

            Vector2 popupPos = new Vector2(absPos.X + circle.X - 100f, absPos.Y + circle.Y - 140f);
            
            popupPos.X = Math.Clamp(popupPos.X, 10f, window.Width - 220f);
            popupPos.Y = Math.Clamp(popupPos.Y, 10f, window.Height - 160f);

            PopupService.ShowPopup(popupBorder, popupPos, _canvas);

            Observable.FromEventPattern(h => okBtn.Click += h, h => okBtn.Click -= h)
                .Subscribe(_ =>
                {
                    PopupService.HidePopup(popupBorder);

                    if (Math.Abs(circle.Radius - initialRadius) > 0.01f)
                    {
                        ExecuteCommand(new ResizeCircleCommand(_canvas, circle, initialRadius, circle.Radius));
                    }
                });
        };
    }
}
