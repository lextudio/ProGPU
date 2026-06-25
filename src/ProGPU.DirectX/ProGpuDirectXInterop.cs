namespace ProGPU.DirectX;

public sealed record ProGpuDirectXNativeHandle(
    string Kind,
    IntPtr Handle,
    bool OwnsHandle = false);

public interface IProGpuDirectXNativeInterop
{
    bool TryGetNativeHandle(ProGpuDirectXResource resource, out ProGpuDirectXNativeHandle handle);
}

public sealed unsafe class ProGpuDirectXBackendInterop : IProGpuDirectXNativeInterop
{
    public bool TryGetNativeHandle(ProGpuDirectXResource resource, out ProGpuDirectXNativeHandle handle)
    {
        ArgumentNullException.ThrowIfNull(resource);

        switch (resource)
        {
            case ProGpuDirectXTexture2D { BackendTexture.TexturePtr: not null } texture:
                handle = new ProGpuDirectXNativeHandle("WebGPU.Texture", (IntPtr)texture.BackendTexture.TexturePtr);
                return true;
            case ProGpuDirectXBuffer { BackendBuffer.BufferPtr: not null } buffer:
                handle = new ProGpuDirectXNativeHandle("WebGPU.Buffer", (IntPtr)buffer.BackendBuffer.BufferPtr);
                return true;
            default:
                handle = new ProGpuDirectXNativeHandle("None", IntPtr.Zero);
                return false;
        }
    }
}
