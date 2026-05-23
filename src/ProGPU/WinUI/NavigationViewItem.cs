using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using ProGPU.Layout;
using ProGPU.Scene;
using ProGPU.Vector;
using ProGPU.Text;

namespace ProGPU.WinUI;

public class NavigationViewItem : Control
{
    private string _text = string.Empty;
    private string _icon = string.Empty;
    private bool _isSelected;
    private bool _isExpanded;
    private int _level;
    private FrameworkElement? _page;

    public string Text
    {
        get => _text;
        set { if (_text != value) { _text = value; Invalidate(); } }
    }

    public string Icon
    {
        get => _icon;
        set { if (_icon != value) { _icon = value; Invalidate(); } }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; Invalidate(); } }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded != value) { _isExpanded = value; Invalidate(); } }
    }

    public int Level
    {
        get => _level;
        internal set { if (_level != value) { _level = value; Invalidate(); } }
    }

    public FrameworkElement? Page
    {
        get => _page;
        set { _page = value; }
    }

    public ObservableCollection<NavigationViewItem> Items { get; }

    public NavigationViewItem()
    {
        Items = new ObservableCollection<NavigationViewItem>();
        Items.CollectionChanged += (s, e) => Invalidate();
        HeightConstraint = 40f;
    }

    public NavigationViewItem(string text, string icon = "", FrameworkElement? page = null) : this()
    {
        Text = text;
        Icon = icon;
        Page = page;
    }

    private NavigationView? FindParentNavigationView()
    {
        var p = Parent;
        while (p != null)
        {
            if (p is NavigationView nav) return nav;
            p = p.Parent;
        }
        return null;
    }

    public override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        if (IsEnabled)
        {
            base.OnPointerPressed(e);
            
            var nav = FindParentNavigationView();
            if (nav != null)
            {
                // In expanded view and clicking on the right expand/collapse indicator (arrow)
                if (Items.Count > 0 && nav.IsPaneOpen && e.Position.X >= Size.X - 40f)
                {
                    IsExpanded = !IsExpanded;
                    nav.OnItemExpandedChanged(this);
                }
                else
                {
                    nav.SelectedItem = this;
                }
                e.Handled = true;
            }
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        float w = WidthConstraint ?? availableSize.X;
        float h = HeightConstraint ?? 40f;
        return new Vector2(w, h);
    }

    protected override void ArrangeOverride(Rect arrangeRect)
    {
        Size = new Vector2(arrangeRect.Width, arrangeRect.Height);
    }

    public override void OnRender(DrawingContext context)
    {
        var nav = FindParentNavigationView();
        bool isPaneOpen = nav?.IsPaneOpen ?? false;
        
        // 1. Draw modern backgrounds depending on active selection or hover
        if (IsSelected)
        {
            context.DrawRectangle(new SolidColorBrush(0xFFFFFF12), null, new Rect(0f, 0f, Size.X, Size.Y));
        }
        else if (IsPointerOver)
        {
            context.DrawRectangle(new SolidColorBrush(0xFFFFFF0F), null, new Rect(0f, 0f, Size.X, Size.Y));
        }

        // 2. Draw 3px left accent stripe indicator
        if (IsSelected)
        {
            context.DrawRectangle(new SolidColorBrush(0x0078D4FF), null, new Rect(3f, 6f, 3f, Size.Y - 12f));
        }

        var font = nav?.GetActiveFont();
        if (font != null)
        {
            float startX = 16f + (Level * 16f); // nesting indentation
            float textY = (Size.Y - 14f) / 2f;

            // 3. Draw Icon in white
            if (!string.IsNullOrEmpty(Icon))
            {
                float startY = (Size.Y - 16f) / 2f;
                bool drewCustomIcon = false;

                if (Icon == "🖱" || Text == "Basic Input")
                {
                    // Clean computer mouse outline (rounded rect) with a scroll wheel line and active left click panel
                    var pen = new Pen(new SolidColorBrush(0xFFFFFFFF), 1f);
                    var mouseOutline = PathGeometry.Parse($"M {startX + 6} {startY + 1} H {startX + 10} Q {startX + 13} {startY + 1} {startX + 13} {startY + 4} V {startY + 12} Q {startX + 13} {startY + 15} {startX + 10} {startY + 15} H {startX + 6} Q {startX + 3} {startY + 15} {startX + 3} {startY + 12} V {startY + 4} Q {startX + 3} {startY + 1} {startX + 6} {startY + 1} Z");
                    context.DrawPath(new SolidColorBrush(0xFFFFFF15), pen, mouseOutline);

                    // Active left click panel (semi-translucent fill)
                    var leftClick = PathGeometry.Parse($"M {startX + 6} {startY + 1} H {startX + 8} V {startY + 8} H {startX + 3} V {startY + 4} Q {startX + 3} {startY + 1} {startX + 6} {startY + 1} Z");
                    context.DrawPath(new SolidColorBrush(0xFFFFFF80), null, leftClick);

                    // Horizontal and vertical split lines
                    var splitLines = PathGeometry.Parse($"M {startX + 3} {startY + 8} H {startX + 13} M {startX + 8} {startY + 1} V {startY + 8}");
                    context.DrawPath(null, pen, splitLines);

                    // Scroll wheel
                    var wheel = PathGeometry.Parse($"M {startX + 7.2f} {startY + 3} H {startX + 8.8f} V {startY + 6} H {startX + 7.2f} Z");
                    context.DrawPath(new SolidColorBrush(0xFFFFFFFF), null, wheel);

                    drewCustomIcon = true;
                }
                else if (Icon == "🔲" || Text == "Layout Panels")
                {
                    // 2x2 grid of small rounded rectangles
                    var gridBrush = new SolidColorBrush(0xFFFFFF30);
                    var gridPen = new Pen(new SolidColorBrush(0xFFFFFFFF), 1f);

                    context.DrawPath(gridBrush, gridPen, CreateRoundedRect(startX + 1f, startY + 1f, 6f, 6f, 1.5f));
                    context.DrawPath(gridBrush, gridPen, CreateRoundedRect(startX + 9f, startY + 1f, 6f, 6f, 1.5f));
                    context.DrawPath(gridBrush, gridPen, CreateRoundedRect(startX + 1f, startY + 9f, 6f, 6f, 1.5f));
                    context.DrawPath(gridBrush, gridPen, CreateRoundedRect(startX + 9f, startY + 9f, 6f, 6f, 1.5f));

                    drewCustomIcon = true;
                }
                else if (Icon == "📄" || Text == "Text & Documents")
                {
                    // Document sheet with folded corner and horizontal lines
                    var docOutline = PathGeometry.Parse($"M {startX + 2} {startY + 1} H {startX + 10} L {startX + 14} {startY + 5} V {startY + 15} H {startX + 2} Z");
                    var docFold = PathGeometry.Parse($"M {startX + 10} {startY + 1} V {startY + 5} H {startX + 14} Z");
                    var docLines = PathGeometry.Parse($"M {startX + 4} {startY + 8} H {startX + 12} M {startX + 4} {startY + 10} H {startX + 12} M {startX + 4} {startY + 12} H {startX + 9}");

                    context.DrawPath(new SolidColorBrush(0xFFFFFF15), new Pen(new SolidColorBrush(0xFFFFFFFF), 1f), docOutline);
                    context.DrawPath(new SolidColorBrush(0xFFFFFFFF), new Pen(new SolidColorBrush(0xFFFFFFFF), 1f), docFold);
                    context.DrawPath(null, new Pen(new SolidColorBrush(0xFFFFFFB0), 1f), docLines);

                    drewCustomIcon = true;
                }
                else if (Icon == "📊" || Text == "Data Virtualization")
                {
                    // Bar chart showing 3 ascending bars
                    var axisPen = new Pen(new SolidColorBrush(0xFFFFFF60), 1f);
                    var axis = PathGeometry.Parse($"M {startX} {startY + 15} H {startX + 16}");
                    context.DrawPath(null, axisPen, axis);

                    var pen = new Pen(new SolidColorBrush(0xFFFFFFFF), 1f);
                    context.DrawPath(new SolidColorBrush(0xFFFFFF60), pen, CreateRoundedRect(startX + 1f, startY + 10f, 3f, 5f, 1f));
                    context.DrawPath(new SolidColorBrush(0xFFFFFF90), pen, CreateRoundedRect(startX + 6f, startY + 5f, 3f, 10f, 1f));
                    context.DrawPath(new SolidColorBrush(0xFFFFFFFF), pen, CreateRoundedRect(startX + 11f, startY + 1f, 3f, 14f, 1f));

                    drewCustomIcon = true;
                }
                else if (Icon == "⚙" || Text == "Compute FX" || Text == "Settings")
                {
                    // Gear cogwheel path using vector geometry
                    var gearGeo = new PathGeometry();
                    var gearFig = new PathFigure(Vector2.Zero) { IsClosed = true };
                    int numTeeth = 8;
                    float cx = startX + 8f;
                    float cy = startY + 8f;
                    for (int i = 0; i < numTeeth; i++)
                    {
                        float angleStart = (float)(i * 2 * Math.PI / numTeeth);
                        float angleMid1 = angleStart + (float)(0.25 * 2 * Math.PI / numTeeth);
                        float angleMid2 = angleStart + (float)(0.55 * 2 * Math.PI / numTeeth);
                        float angleEnd = angleStart + (float)(0.8 * 2 * Math.PI / numTeeth);

                        // Inner base start
                        float x1 = cx + 5f * (float)Math.Cos(angleStart);
                        float y1 = cy + 5f * (float)Math.Sin(angleStart);
                        
                        // Outer tooth start
                        float x2 = cx + 7.5f * (float)Math.Cos(angleMid1);
                        float y2 = cy + 7.5f * (float)Math.Sin(angleMid1);
                        
                        // Outer tooth end
                        float x3 = cx + 7.5f * (float)Math.Cos(angleMid2);
                        float y3 = cy + 7.5f * (float)Math.Sin(angleMid2);
                        
                        // Inner base end
                        float x4 = cx + 5f * (float)Math.Cos(angleEnd);
                        float y4 = cy + 5f * (float)Math.Sin(angleEnd);

                        if (i == 0)
                        {
                            gearFig.StartPoint = new Vector2(x1, y1);
                        }
                        else
                        {
                            gearFig.Segments.Add(new LineSegment(new Vector2(x1, y1)));
                        }
                        gearFig.Segments.Add(new LineSegment(new Vector2(x2, y2)));
                        gearFig.Segments.Add(new LineSegment(new Vector2(x3, y3)));
                        gearFig.Segments.Add(new LineSegment(new Vector2(x4, y4)));
                    }
                    gearGeo.Figures.Add(gearFig);

                    context.DrawPath(new SolidColorBrush(0xFFFFFF30), new Pen(new SolidColorBrush(0xFFFFFFFF), 1f), gearGeo);

                    // Draw inner hole circle of the gear:
                    var innerHole = PathGeometry.Parse($"M {cx - 2f} {cy} Q {cx - 2f} {cy - 2f} {cx} {cy - 2f} Q {cx + 2f} {cy - 2f} {cx + 2f} {cy} Q {cx + 2f} {cy + 2f} {cx} {cy + 2f} Q {cx - 2f} {cy + 2f} {cx - 2f} {cy} Z");
                    context.DrawPath(null, new Pen(new SolidColorBrush(0xFFFFFFFF), 1f), innerHole);

                    drewCustomIcon = true;
                }

                if (!drewCustomIcon)
                {
                    // Fallback to text icon if not matched
                    context.DrawText(Icon, font, 16f, new SolidColorBrush(0xFFFFFFFF), new Vector2(startX, startY));
                }

                startX += 28f;
            }

            // 4. Draw label text in white (or semi-translucent if unselected)
            if (isPaneOpen && !string.IsNullOrEmpty(Text))
            {
                var textBrush = IsSelected ? new SolidColorBrush(0xFFFFFFFF) : new SolidColorBrush(0xFFFFFFD0);
                context.DrawText(Text, font, 14f, textBrush, new Vector2(startX, textY));
            }

            // 5. Draw nested expandable arrow indicator
            if (isPaneOpen && Items.Count > 0)
            {
                string arrow = IsExpanded ? "▼" : "▶";
                context.DrawText(arrow, font, 10f, new SolidColorBrush(0xFFFFFF80), new Vector2(Size.X - 24f, (Size.Y - 10f) / 2f));
            }
        }

        base.OnRender(context);
    }

    private static PathGeometry CreateRoundedRect(float x, float y, float w, float h, float r)
    {
        return PathGeometry.Parse($"M {x+r} {y} H {x+w-r} Q {x+w} {y} {x+w} {y+r} V {y+h-r} Q {x+w} {y+h} {x+w-r} {y+h} H {x+r} Q {x} {y+h} {x} {y+h-r} V {y+r} Q {x} {y} {x+r} {y} Z");
    }
}
