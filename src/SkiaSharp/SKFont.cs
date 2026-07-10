using System;
using System.Numerics;
using ProGPU.Vector;

namespace SkiaSharp;

public class SKFont : IDisposable
{
    public SKTypeface Typeface { get; set; }
    public float Size { get; set; }
    public SKFontHinting Hinting { get; set; } = SKFontHinting.Normal;
    public SKFontEdging Edging { get; set; } = SKFontEdging.Antialias;
    public bool Subpixel { get; set; } = true;
    public bool BaselineSnap { get; set; } = true;
    public bool ForceAutoHinting { get; set; } = false;
    public bool LinearMetrics { get; set; }
    public bool Embolden { get; set; }
    public float ScaleX { get; set; } = 1f;
    public float SkewX { get; set; }

    public SKFont(SKTypeface typeface, float size = 12f, float scaleX = 1f, float skewX = 0f)
    {
        Typeface = typeface;
        Size = size;
        ScaleX = scaleX;
        SkewX = skewX;
    }

    public SKPath? GetGlyphPath(ushort glyphId)
    {
        var outline = Typeface.Font.GetFlippedGlyphOutline(glyphId);
        if (outline == null) return null;

        var path = new SKPath();
        float scale = Size / Typeface.Font.UnitsPerEm;

        foreach (var figure in outline.Figures)
        {
            var start = figure.StartPoint * scale;
            path.MoveTo(start.X, start.Y);

            foreach (var segment in figure.Segments)
            {
                if (segment is LineSegment line)
                {
                    var pt = line.Point * scale;
                    path.LineTo(pt.X, pt.Y);
                }
                else if (segment is QuadraticBezierSegment quad)
                {
                    var ctrl = quad.ControlPoint * scale;
                    var pt = quad.Point * scale;
                    path.QuadTo(ctrl.X, ctrl.Y, pt.X, pt.Y);
                }
                else if (segment is CubicBezierSegment cubic)
                {
                    var ctrl1 = cubic.ControlPoint1 * scale;
                    var ctrl2 = cubic.ControlPoint2 * scale;
                    var pt = cubic.Point * scale;
                    path.CubicTo(ctrl1.X, ctrl1.Y, ctrl2.X, ctrl2.Y, pt.X, pt.Y);
                }
                else if (segment is ArcSegment arc)
                {
                    var pt = arc.Point * scale;
                    var arcSize = arc.Size * scale;
                    path.ArcTo(arcSize.X, arcSize.Y, arc.RotationAngle, 
                        arc.IsLargeArc ? SKPathArcSize.Large : SKPathArcSize.Small,
                        arc.SweepDirection == SweepDirection.Clockwise ? SKPathDirection.Clockwise : SKPathDirection.CounterClockwise,
                        pt.X, pt.Y);
                }
            }

            if (figure.IsClosed)
            {
                path.Close();
            }
        }

        return path;
    }

    public void GetGlyphWidths(ReadOnlySpan<ushort> glyphs, Span<float> widths, Span<SKRect> bounds)
    {
        for (int i = 0; i < glyphs.Length; i++)
        {
            ushort glyphId = glyphs[i];
            
            float advance = Typeface.Font.GetAdvanceWidth(glyphId, Size);
            if (!widths.IsEmpty)
            {
                widths[i] = advance;
            }

            if (!bounds.IsEmpty)
            {
                var outline = Typeface.Font.GetFlippedGlyphOutline(glyphId);
                if (outline == null)
                {
                    bounds[i] = SKRect.Empty;
                }
                else
                {
                    float scale = Size / Typeface.Font.UnitsPerEm;
                    float minX = float.MaxValue, maxX = float.MinValue;
                    float minY = float.MaxValue, maxY = float.MinValue;
                    bool hasPoints = false;

                    void ProcessPt(Vector2 pt)
                    {
                        float sx = pt.X * scale;
                        float sy = pt.Y * scale;
                        minX = Math.Min(minX, sx);
                        maxX = Math.Max(maxX, sx);
                        minY = Math.Min(minY, sy);
                        maxY = Math.Max(maxY, sy);
                        hasPoints = true;
                    }

                    foreach (var figure in outline.Figures)
                    {
                        ProcessPt(figure.StartPoint);
                        foreach (var segment in figure.Segments)
                        {
                            if (segment is LineSegment line)
                            {
                                ProcessPt(line.Point);
                            }
                            else if (segment is QuadraticBezierSegment quad)
                            {
                                ProcessPt(quad.ControlPoint);
                                ProcessPt(quad.Point);
                            }
                            else if (segment is CubicBezierSegment cubic)
                            {
                                ProcessPt(cubic.ControlPoint1);
                                ProcessPt(cubic.ControlPoint2);
                                ProcessPt(cubic.Point);
                            }
                            else if (segment is ArcSegment arc)
                            {
                                ProcessPt(arc.Point);
                            }
                        }
                    }

                    if (!hasPoints)
                    {
                        bounds[i] = new SKRect(0, 0, advance, Size);
                    }
                    else
                    {
                        bounds[i] = new SKRect(minX, minY, maxX, maxY);
                    }
                }
            }
        }
    }

    public void Dispose() { }
}
