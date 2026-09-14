using ProGPU.Backend;
using Silk.NET.WebGPU;

namespace ProGPU.GameEngine.Rendering;

/// <summary>
/// Device-owned full-resolution color, optional MSAA color and depth attachments.
/// Creation/resize is bounded by an explicit byte budget; stable reuse is O(1).
/// The caller owns command encoding/submission and must finish using attachments
/// before resizing. Backend deferred disposal protects earlier GPU submissions.
/// </summary>
public sealed class SceneRenderTarget : IDisposable
{
    private readonly WgpuContext _context;
    public GpuTexture? Color { get; private set; }
    public GpuTexture? MultisampleColor { get; private set; }
    public GpuTexture? Depth { get; private set; }
    public long ResidentBytes { get; private set; }
    public uint SampleCount { get; private set; }
    public uint Generation { get; private set; }
    public long ByteBudget { get; }
    private bool _disposed;

    public SceneRenderTarget(WgpuContext context, long byteBudget)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (byteBudget <= 0) throw new ArgumentOutOfRangeException(nameof(byteBudget));
        _context = context; ByteBudget = byteBudget;
    }

    public static long EstimateBytes(uint width, uint height, uint samples)
    {
        if (width == 0 || height == 0 || samples is not (1 or 4)) throw new ArgumentOutOfRangeException(nameof(width));
        // Supported color formats and Depth32Float each use four bytes/sample.
        return checked((long)width * height * (4 + 4 * samples + (samples == 1 ? 0 : 4 * samples)));
    }

    /// <summary>False means the exact requested resolution exceeds the budget; it is never downscaled.</summary>
    public bool TryEnsure(uint width, uint height, uint samples, TextureFormat format)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (format is not (TextureFormat.Bgra8Unorm or TextureFormat.Bgra8UnormSrgb or TextureFormat.Rgba8Unorm or TextureFormat.Rgba8UnormSrgb))
            throw new ArgumentOutOfRangeException(nameof(format));
        long bytes = EstimateBytes(width, height, samples);
        if (bytes > ByteBudget) { Release(); return false; }
        if (Color is { } current && current.Width == width && current.Height == height && current.Format == format && SampleCount == samples)
            return true;
        // Release old residency before creating replacements; resize cannot double
        // the logical budget. In-flight physical residency depends on the GPU fence.
        Release();
        try
        {
            Color = new(_context, width, height, format, TextureUsage.RenderAttachment | TextureUsage.TextureBinding,
                "Game scene resolve", alphaMode: GpuTextureAlphaMode.Premultiplied);
            if (samples > 1)
                MultisampleColor = new(_context, width, height, format, TextureUsage.RenderAttachment,
                    "Game scene multisample color", samples, GpuTextureAlphaMode.Premultiplied);
            Depth = new(_context, width, height, TextureFormat.Depth32float, TextureUsage.RenderAttachment,
                "Game scene depth", samples);
            SampleCount = samples; ResidentBytes = bytes; Generation++;
            return true;
        }
        catch { Release(); throw; }
    }

    public void Release()
    {
        Color?.Dispose(); MultisampleColor?.Dispose(); Depth?.Dispose();
        Color = MultisampleColor = Depth = null; ResidentBytes = 0; SampleCount = 0;
    }
    public void Dispose() { if (_disposed) return; Release(); _disposed = true; }
}
