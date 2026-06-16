using System;
using System.Collections.Concurrent;
using ProGPU.Text;
using System.Windows;

namespace System.Windows.Media;

public class FontFamily
{
    private readonly record struct FontCacheEntry(string FilePath, int FaceIndex);

    private static readonly ConcurrentDictionary<string, FontCacheEntry> s_fontCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, TtfFont> s_ttfCache = new(StringComparer.OrdinalIgnoreCase);
    private static FontCacheEntry? s_fallbackFont;

    static FontFamily()
    {
        try
        {
            var systemFonts = FontApi.GetSystemFonts();
            foreach (var font in systemFonts)
            {
                if (!string.IsNullOrEmpty(font.FamilyName))
                {
                    s_fontCache.TryAdd(font.FamilyName, new FontCacheEntry(font.FilePath, font.FaceIndex));
                }
                if (!string.IsNullOrEmpty(font.Name))
                {
                    s_fontCache.TryAdd(font.Name, new FontCacheEntry(font.FilePath, font.FaceIndex));
                }
            }

            foreach (var key in new[] { "Arial", "Consolas", "Georgia", "Helvetica", "Roboto", "Courier New", "Times New Roman" })
            {
                if (s_fontCache.TryGetValue(key, out var entry))
                {
                    s_fallbackFont = entry;
                    break;
                }
            }

            if (s_fallbackFont == null && systemFonts.Count > 0)
            {
                s_fallbackFont = new FontCacheEntry(systemFonts[0].FilePath, systemFonts[0].FaceIndex);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WPF FontFamily] Error initializing system font cache: {ex.Message}");
        }
    }

    public string Name { get; }
    internal string FilePath { get; }
    internal int FaceIndex { get; }
    private string CacheKey => $"{FilePath}\u001f{FaceIndex}";

    public TtfFont? NativeFont
    {
        get
        {
            if (string.IsNullOrEmpty(FilePath)) return null;
            return s_ttfCache.GetOrAdd(CacheKey, _ => new TtfFont(FilePath, FaceIndex));
        }
    }

    public FontFamily(string name)
    {
        Name = name;
        if (s_fontCache.TryGetValue(name, out var entry))
        {
            FilePath = entry.FilePath;
            FaceIndex = entry.FaceIndex;
        }
        else
        {
            FilePath = s_fallbackFont?.FilePath ?? "";
            FaceIndex = s_fallbackFont?.FaceIndex ?? 0;
        }
    }
}

public class Typeface
{
    public FontFamily FontFamily { get; }
    public FontStyle Style { get; }
    public FontWeight Weight { get; }
    public FontStretch Stretch { get; }

    public Typeface(FontFamily fontFamily)
    {
        FontFamily = fontFamily;
    }

    public Typeface(FontFamily fontFamily, FontStyle style, FontWeight weight, FontStretch stretch)
    {
        FontFamily = fontFamily;
        Style = style;
        Weight = weight;
        Stretch = stretch;
    }
}
