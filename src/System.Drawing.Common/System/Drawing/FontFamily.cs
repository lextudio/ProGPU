using System.Collections.Concurrent;
using ProGPU.Text;

namespace System.Drawing;

public class FontFamily : IDisposable
{
    private readonly record struct FontCacheEntry(string FilePath, int FaceIndex);

    private static readonly ConcurrentDictionary<string, FontCacheEntry> s_fontCache = new(StringComparer.OrdinalIgnoreCase);
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

            foreach (var key in new[] { "Arial", "Consolas", "Georgia", "Helvetica", "Roboto", "Courier New" })
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
            Console.WriteLine($"[FontFamily] Error initializing system font cache: {ex.Message}");
        }
    }

    public string Name { get; }
    internal string FilePath { get; }
    internal int FaceIndex { get; }
    internal string CacheKey => $"{FilePath}\u001f{FaceIndex}";

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

    public static FontFamily GenericSansSerif { get; } = new FontFamily("Arial");
    public static FontFamily GenericSerif { get; } = new FontFamily("Georgia");
    public static FontFamily GenericMonospace { get; } = new FontFamily("Courier New");

    public void Dispose() {}
}
