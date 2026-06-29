namespace ProGPU.Backend;

public interface IProGpuTextureSource
{
    bool TryGetGpuTexture(out GpuTexture texture);
}
