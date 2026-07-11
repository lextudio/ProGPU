using ProGPU.Backend;
using ProGPU.Scene;
using Silk.NET.WebGPU;

namespace System.Drawing;

internal static class GpuProvider
{
    private static readonly object s_compositorCacheScope = new();
    private static WgpuContext? _context;

    public static WgpuContext Context
    {
        get
        {
            var current = WgpuContext.Current;
            if (current != null && current.IsInitialized)
            {
                return current;
            }

            lock (s_compositorCacheScope)
            {
                if (_context != null && _context.IsInitialized)
                {
                    return _context;
                }
                if (_context != null)
                {
                    try { _context.Dispose(); } catch {}
                }
                _context = new WgpuContext();
                _context.Initialize(null);
                return _context;
            }
        }
    }

    public static Compositor Compositor
    {
        get
        {
            return GetCompositor(Context);
        }
    }

    public static Compositor GetCompositor(WgpuContext context)
    {
        if (context == null || context.IsDisposed)
        {
            throw new InvalidOperationException("Cannot create a compositor for a disposed or missing WebGPU context.");
        }

        return SharedCompositorCache.GetOrCreate(context, TextureFormat.Rgba8Unorm, s_compositorCacheScope);
    }
}
