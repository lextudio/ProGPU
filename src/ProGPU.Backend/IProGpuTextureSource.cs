namespace ProGPU.Backend;

public interface IProGpuTextureSource
{
    bool TryGetGpuTexture(out GpuTexture texture);
}

/// <summary>
/// Keeps a caller-owned GPU texture alive while deferred render commands use it.
/// Disposing the lease releases only the borrow; ownership of the texture stays
/// with the source that created the lease.
/// </summary>
public interface IProGpuTextureLease : IDisposable
{
    GpuTexture Texture { get; }
}

/// <summary>
/// Provides a typed lifetime lease for a GPU texture that can be referenced by
/// deferred render commands without copying or taking ownership of the texture.
/// </summary>
public interface IProGpuTextureLeaseSource : IProGpuTextureSource
{
    bool TryAcquireGpuTextureLease(out IProGpuTextureLease lease);
}

/// <summary>
/// Materializes and leases a texture in the context that will consume it.
/// CPU-backed or otherwise portable image sources use this seam to avoid
/// allocating a texture before a presentation host has selected its device.
/// </summary>
public interface IProGpuContextTextureLeaseSource : IProGpuTextureLeaseSource
{
    bool TryGetGpuTexture(WgpuContext requiredContext, out GpuTexture texture);

    bool TryAcquireGpuTextureLease(
        WgpuContext requiredContext,
        out IProGpuTextureLease lease);
}
