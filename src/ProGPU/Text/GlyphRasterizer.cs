using System;
using System.Collections.Generic;
using System.Numerics;
using ProGPU.Vector;

namespace ProGPU.Text;

public struct RasterGlyph
{
    public int Width;
    public int Height;
    public int BearX;
    public int BearY;
    public byte[] AlphaMap;
}

public static class GlyphRasterizer
{
    public static RasterGlyph Rasterize(PathGeometry outline, TtfFont font, float emSize)
    {
        // 1. Calculate scaling factors using the exact target emSize
        float scale = emSize / font.UnitsPerEm;

        // Extract and flatten contours, scaling coordinates with a fine tolerance of 0.15f
        var flattenedContours = outline.Flatten(0.15f);
        var scaledContours = new List<List<Vector2>>();
        
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        // Convert TTF coordinates (Y-up) to screen coordinates (Y-down) and find bounds
        foreach (var contour in flattenedContours)
        {
            var scaledContour = new List<Vector2>(contour.Count);
            foreach (var pt in contour)
            {
                var spt = new Vector2(pt.X * scale, -pt.Y * scale);
                scaledContour.Add(spt);

                minX = Math.Min(minX, spt.X);
                maxX = Math.Max(maxX, spt.X);
                minY = Math.Min(minY, spt.Y);
                maxY = Math.Max(maxY, spt.Y);
            }
            scaledContours.Add(scaledContour);
        }

        // If glyph is empty
        if (scaledContours.Count == 0 || minX > maxX || minY > maxY)
        {
            return new RasterGlyph { Width = 0, Height = 0, BearX = 0, BearY = 0, AlphaMap = Array.Empty<byte>() };
        }

        // 2. Add padding/margin of 4px on all sides of the glyph bounding box for perfect AA
        int padding = 4;
        int xStart = (int)Math.Floor(minX) - padding;
        int xEnd = (int)Math.Ceiling(maxX) + padding;
        int yStart = (int)Math.Floor(minY) - padding;
        int yEnd = (int)Math.Ceiling(maxY) + padding;

        int width = xEnd - xStart;
        int height = yEnd - yStart;

        if (width <= 0 || height <= 0)
        {
            return new RasterGlyph { Width = 0, Height = 0, BearX = 0, BearY = 0, AlphaMap = Array.Empty<byte>() };
        }

        byte[] alphaMap = new byte[width * height];

        // 3. Ray-casting polygon intersection checker with Even-Odd rule
        bool IsPointInContours(Vector2 p)
        {
            int winding = 0;
            foreach (var contour in scaledContours)
            {
                int count = contour.Count;
                if (count < 2) continue;

                for (int i = 0; i < count; i++)
                {
                    Vector2 v1 = contour[i];
                    Vector2 v2 = contour[(i + 1) % count];
                    if (v1 == v2) continue;

                    if (v1.Y <= p.Y)
                    {
                        if (v2.Y > p.Y) // Upward crossing
                        {
                            float intersectX = v1.X + (p.Y - v1.Y) * (v2.X - v1.X) / (v2.Y - v1.Y);
                            if (p.X < intersectX)
                            {
                                winding++;
                            }
                        }
                    }
                    else
                    {
                        if (v2.Y <= p.Y) // Downward crossing
                        {
                            float intersectX = v1.X + (p.Y - v1.Y) * (v2.X - v1.X) / (v2.Y - v1.Y);
                            if (p.X < intersectX)
                            {
                                winding--;
                            }
                        }
                    }
                }
            }
            return winding != 0;
        }

        // 4. Evaluate subpixel coverage using 4x SSAA (supersampling) around +0.5 per pixel
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                float coverage = 0.0f;
                if (IsPointInContours(new Vector2(xStart + x + 0.25f, yStart + y + 0.25f))) coverage += 0.25f;
                if (IsPointInContours(new Vector2(xStart + x + 0.75f, yStart + y + 0.25f))) coverage += 0.25f;
                if (IsPointInContours(new Vector2(xStart + x + 0.25f, yStart + y + 0.75f))) coverage += 0.25f;
                if (IsPointInContours(new Vector2(xStart + x + 0.75f, yStart + y + 0.75f))) coverage += 0.25f;

                alphaMap[rowOffset + x] = (byte)Math.Round(coverage * 255.0f);
            }
        }

        return new RasterGlyph
        {
            Width = width,
            Height = height,
            BearX = xStart,
            BearY = yStart,
            AlphaMap = alphaMap
        };
    }
}
