namespace ProGPU.Backend;

/// <summary>
/// Exposes the native handles used to present a framebuffer through a ProGPU surface.
/// </summary>
public interface IProGpuSurfaceFramebuffer
{
    /// <summary>
    /// Gets the native WebGPU surface pointer.
    /// </summary>
    nint SurfacePointer { get; }

    /// <summary>
    /// Gets the native window pointer associated with the surface.
    /// </summary>
    nint WindowPointer { get; }
}
