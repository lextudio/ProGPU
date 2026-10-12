using SW = Silk.NET.WebGPU;
using W = WebGpuSharp;

namespace ProGPU.Backend.Dawn;

internal static class DawnSurfacePresentModePolicy
{
    // The existing WgpuContext policy: prefer Immediate when uncapped, then
    // FIFO, then the first advertised mode. Validate the complete inventory
    // before selecting; an absent inventory must never invent support.
    // O(N) only at surface creation; resize and VSync changes use cached values.
    internal static W.PresentMode Select(bool vsync, ReadOnlySpan<W.PresentMode> modes)
    {
        if (modes.IsEmpty)
            throw new NotSupportedException("The Dawn surface advertises no presentation modes.");

        bool immediate = false;
        bool fifo = false;
        foreach (W.PresentMode mode in modes)
        {
            _ = ToSilk(mode);
            immediate |= mode == W.PresentMode.Immediate;
            fifo |= mode == W.PresentMode.Fifo;
        }
        if (!vsync && immediate) return W.PresentMode.Immediate;
        if (fifo) return W.PresentMode.Fifo;
        return modes[0];
    }

    internal static SW.PresentMode ToSilk(W.PresentMode mode) => mode switch
    {
        W.PresentMode.Fifo => SW.PresentMode.Fifo,
        W.PresentMode.FifoRelaxed => SW.PresentMode.FifoRelaxed,
        W.PresentMode.Immediate => SW.PresentMode.Immediate,
        W.PresentMode.Mailbox => SW.PresentMode.Mailbox,
        _ => throw new NotSupportedException($"Unsupported Dawn presentation mode {mode}.")
    };
}
