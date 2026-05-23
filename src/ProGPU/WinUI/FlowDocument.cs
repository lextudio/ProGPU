using System;
using System.Collections.Generic;
using System.Numerics;
using ProGPU.Layout;
using ProGPU.Vector;
using ProGPU.Scene;
using ProGPU.Text;

namespace ProGPU.WinUI;

public class Paragraph
{
    public List<Inline> Inlines { get; } = new();
    public float MarginBottom { get; set; } = 12f;
    public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;

    public Paragraph() { }
    public Paragraph(params Inline[] inlines)
    {
        Inlines.AddRange(inlines);
    }
}

public class FlowDocument : FrameworkElement
{
    private TtfFont? _font;
    private float _fontSize = 14f;
    private int _columnCount = 2;
    private float _columnGap = 24f;
    private readonly List<PositionedRichChar> _positionedChars = new();
    private Hyperlink? _hoveredHyperlink = null;

    public List<Paragraph> Paragraphs { get; } = new();

    public TtfFont? Font
    {
        get => _font;
        set { _font = value; Invalidate(); }
    }

    public float FontSize
    {
        get => _fontSize;
        set { _fontSize = value; Invalidate(); }
    }

    private Brush? _foreground;
    public Brush? Foreground
    {
        get => _foreground;
        set { _foreground = value; Invalidate(); }
    }

    public int ColumnCount
    {
        get => _columnCount;
        set { _columnCount = Math.Max(1, value); Invalidate(); }
    }

    public float ColumnGap
    {
        get => _columnGap;
        set { _columnGap = value; Invalidate(); }
    }

    public FlowDocument()
    {
        Padding = new Thickness(16);
    }

    public override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!IsEnabled) return;

        var localPos = InputSystem.GetLocalPosition(this, e.Position);
        Hyperlink? foundLink = null;

        foreach (var pc in _positionedChars)
        {
            if (pc.Info.SourceInline is Hyperlink hl && Font != null)
            {
                ushort gIdx = Font.GetGlyphIndex(pc.Info.Character);
                float advance = Font.GetAdvanceWidth(gIdx, pc.Info.FontSize);
                Rect charRect = new Rect(pc.Position.X, pc.Position.Y, advance, pc.Info.FontSize);
                if (charRect.Contains(localPos))
                {
                    foundLink = hl;
                    break;
                }
            }
        }

        if (_hoveredHyperlink != foundLink)
        {
            _hoveredHyperlink = foundLink;
            Invalidate();
        }
    }

    public override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsEnabled && _hoveredHyperlink != null)
        {
            _hoveredHyperlink.RaiseClick();
            e.Handled = true;
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        float w = WidthConstraint ?? availableSize.X;
        float h = HeightConstraint ?? availableSize.Y;
        if (float.IsInfinity(w)) w = 600f;
        if (float.IsInfinity(h)) h = 400f;

        PerformFlowLayout(w, h);
        return new Vector2(w, h);
    }

    protected override void ArrangeOverride(Rect arrangeRect)
    {
        Size = new Vector2(arrangeRect.Width, arrangeRect.Height);
        PerformFlowLayout(arrangeRect.Width, arrangeRect.Height);

        // Arrange nested child controls
        foreach (var pc in _positionedChars)
        {
            if (pc.Info.EmbeddedElement != null)
            {
                var child = pc.Info.EmbeddedElement;
                child.Arrange(new Rect(pc.Position.X, pc.Position.Y, child.DesiredSize.X, child.DesiredSize.Y));
            }
        }
    }

    private void PerformFlowLayout(float width, float height)
    {
        _positionedChars.Clear();
        if (Font == null || Paragraphs.Count == 0 || width <= 0f || height <= 0f) return;

        float scale = FontSize / Font.UnitsPerEm;
        float lineSpacing = (Font.Ascender - Font.Descender + Font.LineGap) * scale;

        float availableWidth = width - Padding.Horizontal;
        float colWidth = (availableWidth - (ColumnCount - 1) * ColumnGap) / ColumnCount;
        float colHeight = height - Padding.Vertical;

        int currentColumn = 0;
        float cursorX = Padding.Left;
        float cursorY = Padding.Top;

        var defaultFg = Foreground ?? new SolidColorBrush(0xFFFFFFFF);

        var currentChildren = new List<Visual>(Children);
        var encounteredChildren = new HashSet<Visual>();

        foreach (var paragraph in Paragraphs)
        {
            var charList = new List<RichChar>();
            foreach (var inline in paragraph.Inlines)
            {
                AccumulateInlines(inline, charList, defaultFg, FontSize, false, false, false, null);
            }

            if (charList.Count == 0) continue;

            var paragraphLines = new List<List<PositionedRichChar>>();
            int i = 0;
            while (i < charList.Count)
            {
                if (cursorY + lineSpacing > Padding.Top + colHeight)
                {
                    currentColumn++;
                    if (currentColumn >= ColumnCount)
                    {
                        break;
                    }
                    cursorX = Padding.Left + currentColumn * (colWidth + ColumnGap);
                    cursorY = Padding.Top;
                }

                var lineChars = new List<RichChar>();
                float lineW = 0f;
                int lastWordIdx = -1;

                while (i < charList.Count)
                {
                    var rc = charList[i];
                    float advance = 0f;
                    if (rc.EmbeddedElement != null)
                    {
                        var child = rc.EmbeddedElement;
                        encounteredChildren.Add(child);
                        if (child.Parent != this)
                        {
                            AddChild(child);
                        }
                        child.Measure(new Vector2(colWidth, float.PositiveInfinity));
                        advance = child.DesiredSize.X + 4f;
                    }
                    else
                    {
                        ushort gIdx = Font.GetGlyphIndex(rc.Character);
                        advance = Font.GetAdvanceWidth(gIdx, rc.FontSize);
                    }

                    if (rc.Character == ' ' || rc.Character == '\t')
                    {
                        lastWordIdx = lineChars.Count;
                    }

                    if (lineW + advance > colWidth && lineChars.Count > 0)
                    {
                        if (lastWordIdx > 0 && lastWordIdx < lineChars.Count)
                        {
                            int diff = lineChars.Count - lastWordIdx;
                            lineChars.RemoveRange(lastWordIdx, diff);
                            i -= diff;
                        }
                        break;
                    }

                    lineChars.Add(rc);
                    lineW += advance;
                    i++;
                }

                var currentLine = new List<PositionedRichChar>();
                float runningX = cursorX;
                foreach (var rc in lineChars)
                {
                    float advance = 0f;
                    if (rc.EmbeddedElement != null)
                    {
                        advance = rc.EmbeddedElement.DesiredSize.X + 4f;
                    }
                    else
                    {
                        ushort gIdx = Font.GetGlyphIndex(rc.Character);
                        advance = Font.GetAdvanceWidth(gIdx, rc.FontSize);
                    }

                    currentLine.Add(new PositionedRichChar
                    {
                        Info = rc,
                        Position = new Vector2(runningX, cursorY)
                    });
                    runningX += advance;
                }

                paragraphLines.Add(currentLine);
                cursorY += lineSpacing;
            }

            // Apply horizontal alignments inside this paragraph's lines
            for (int l = 0; l < paragraphLines.Count; l++)
            {
                var line = paragraphLines[l];
                if (line.Count == 0) continue;

                float lineW = 0f;
                var lastPc = line[^1];
                float lastAdv = 0f;
                if (lastPc.Info.EmbeddedElement != null)
                {
                    lastAdv = lastPc.Info.EmbeddedElement.DesiredSize.X + 4f;
                }
                else
                {
                    lastAdv = Font.GetAdvanceWidth(Font.GetGlyphIndex(lastPc.Info.Character), lastPc.Info.FontSize);
                }
                lineW = lastPc.Position.X + lastAdv - cursorX;

                float shiftX = 0f;
                if (paragraph.TextAlignment == TextAlignment.Right)
                {
                    shiftX = colWidth - lineW;
                }
                else if (paragraph.TextAlignment == TextAlignment.Center)
                {
                    shiftX = (colWidth - lineW) / 2f;
                }
                else if (paragraph.TextAlignment == TextAlignment.Justify)
                {
                    bool isLastLine = (l == paragraphLines.Count - 1);
                    int spaceCount = 0;
                    for (int k = 0; k < line.Count - 1; k++)
                    {
                        if (line[k].Info.Character == ' ' || line[k].Info.Character == '\t')
                            spaceCount++;
                    }

                    if (!isLastLine && spaceCount > 0 && lineW < colWidth)
                    {
                        float extraW = colWidth - lineW;
                        float spaceAddition = extraW / spaceCount;
                        float runningAddition = 0f;
                        for (int k = 0; k < line.Count; k++)
                        {
                            var pc = line[k];
                            pc.Position.X += runningAddition;
                            if (pc.Info.Character == ' ' || pc.Info.Character == '\t')
                            {
                                runningAddition += spaceAddition;
                            }
                        }
                    }
                }

                if (shiftX > 0f && !float.IsInfinity(shiftX))
                {
                    foreach (var pc in line)
                    {
                        pc.Position.X += shiftX;
                    }
                }

                _positionedChars.AddRange(line);
            }

            cursorY += paragraph.MarginBottom;
            if (currentColumn >= ColumnCount) break;
        }

        // Clean up children that are no longer referenced
        foreach (var child in currentChildren)
        {
            if (child is FrameworkElement fe && !encounteredChildren.Contains(fe))
            {
                RemoveChild(fe);
            }
        }
    }

    private void AccumulateInlines(Inline inline, List<RichChar> list, Brush defaultFg, float defaultSize, bool isBold, bool isItalic, bool isUnderline, Inline? parentInline)
    {
        Brush fg = inline.Foreground ?? defaultFg;
        float size = inline.FontSize ?? defaultSize;
        Inline source = parentInline ?? inline;

        if (inline is Run run)
        {
            foreach (char c in run.Text)
            {
                list.Add(new RichChar
                {
                    Character = c,
                    Foreground = fg,
                    FontSize = size,
                    IsBold = isBold,
                    IsItalic = isItalic,
                    IsUnderline = isUnderline,
                    SourceInline = source
                });
            }
        }
        else if (inline is InlineUIContainer uic)
        {
            list.Add(new RichChar
            {
                Character = '\uFFFC',
                Foreground = fg,
                FontSize = size,
                IsBold = isBold,
                IsItalic = isItalic,
                IsUnderline = isUnderline,
                SourceInline = uic,
                EmbeddedElement = uic.Child
            });
        }
        else if (inline is Span span)
        {
            bool nextBold = isBold || (span is Bold);
            bool nextItalic = isItalic || (span is Italic);
            bool nextUnderline = isUnderline || (span is Underline || span is Hyperlink);

            if (span is Hyperlink && inline.Foreground == null)
            {
                fg = new SolidColorBrush(0x0078D4FF);
            }

            foreach (var sub in span.Inlines)
            {
                AccumulateInlines(sub, list, fg, size, nextBold, nextItalic, nextUnderline, span is Hyperlink ? span : source);
            }
        }
    }

    public override void OnRender(DrawingContext context)
    {
        if (Font == null || _positionedChars.Count == 0) return;

        // Group consecutive characters of same style for extreme text composition speed
        string runBuffer = "";
        Vector2 startPos = Vector2.Zero;
        RichChar style = default;

        foreach (var pc in _positionedChars)
        {
            if (pc.Info.EmbeddedElement != null)
            {
                if (runBuffer.Length > 0)
                {
                    RenderRun(context, runBuffer, startPos, style);
                    runBuffer = "";
                }
                continue;
            }

            var pcStyle = pc.Info;
            if (pc.Info.SourceInline is Hyperlink hl && hl == _hoveredHyperlink)
            {
                pcStyle.Foreground = new SolidColorBrush(0x005A9EFF);
            }

            if (runBuffer.Length == 0)
            {
                runBuffer = pc.Info.Character.ToString();
                startPos = pc.Position;
                style = pcStyle;
            }
            else if (pcStyle.IsBold == style.IsBold &&
                     pcStyle.IsItalic == style.IsItalic &&
                     pcStyle.IsUnderline == style.IsUnderline &&
                     pcStyle.FontSize == style.FontSize &&
                     pcStyle.Foreground.Equals(style.Foreground) &&
                     pcStyle.SourceInline == style.SourceInline &&
                     Math.Abs(pc.Position.Y - startPos.Y) < 1f)
            {
                runBuffer += pc.Info.Character;
            }
            else
            {
                RenderRun(context, runBuffer, startPos, style);
                runBuffer = pc.Info.Character.ToString();
                startPos = pc.Position;
                style = pcStyle;
            }
        }

        if (runBuffer.Length > 0)
        {
            RenderRun(context, runBuffer, startPos, style);
        }

        base.OnRender(context);
    }

    private void RenderRun(DrawingContext context, string runBuffer, Vector2 startPos, RichChar style)
    {
        if (Font == null) return;
        context.DrawText(runBuffer, Font, style.FontSize, style.Foreground, startPos);
        if (style.IsUnderline)
        {
            float runW = 0f;
            foreach (char c in runBuffer)
            {
                runW += Font.GetAdvanceWidth(Font.GetGlyphIndex(c), style.FontSize);
            }
            context.DrawRectangle(style.Foreground, null, new Rect(startPos.X, startPos.Y + style.FontSize - 1f, runW, 1f));
        }
    }
}
