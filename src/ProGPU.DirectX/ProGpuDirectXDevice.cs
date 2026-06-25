using ProGPU.Backend;

namespace ProGPU.DirectX;

public sealed class ProGpuDirectXDevice : IDisposable
{
    private bool _isDisposed;

    private ProGpuDirectXDevice(WgpuContext? context, ProGpuDirectXDeviceOptions options)
    {
        Context = context;
        Options = options;
        Capabilities = new ProGpuDirectXCapabilities(
            isGpuBacked: context is { IsDisposed: false, Device: not null, Queue: not null },
            maxTextureDimension2D: 16384);

        if (options.RequireGpuBackedResources && !Capabilities.IsGpuBacked)
        {
            throw new InvalidOperationException("A GPU-backed WgpuContext is required for this DirectX device.");
        }

        if (!Capabilities.SupportsFeatureLevel(options.MinimumFeatureLevel))
        {
            throw new NotSupportedException($"DirectX feature level {options.MinimumFeatureLevel} is not supported by the ProGPU shim.");
        }
    }

    public WgpuContext? Context { get; }

    public ProGpuDirectXDeviceOptions Options { get; }

    public ProGpuDirectXCapabilities Capabilities { get; }

    public bool IsGpuBacked => Capabilities.IsGpuBacked;

    public static ProGpuDirectXDevice CreateMetadataDevice(ProGpuDirectXDeviceOptions? options = null)
    {
        return new ProGpuDirectXDevice(null, options ?? new ProGpuDirectXDeviceOptions());
    }

    public static ProGpuDirectXDevice FromCurrentContext(ProGpuDirectXDeviceOptions? options = null)
    {
        return FromContext(WgpuContext.Current, options);
    }

    public static ProGpuDirectXDevice FromContext(WgpuContext? context, ProGpuDirectXDeviceOptions? options = null)
    {
        return new ProGpuDirectXDevice(context, options ?? new ProGpuDirectXDeviceOptions());
    }

    public ProGpuDirectXDeviceContext CreateImmediateContext()
    {
        ThrowIfDisposed();
        return new ProGpuDirectXDeviceContext(this);
    }

    public ProGpuDirectXBuffer CreateBuffer(DxBufferDescriptor descriptor)
    {
        ThrowIfDisposed();
        return new ProGpuDirectXBuffer(this, descriptor);
    }

    public ProGpuDirectXTexture2D CreateTexture2D(DxTexture2DDescriptor descriptor)
    {
        ThrowIfDisposed();
        return new ProGpuDirectXTexture2D(this, descriptor);
    }

    public ProGpuDirectXSwapChain CreateSwapChain(DxSwapChainDescriptor descriptor)
    {
        ThrowIfDisposed();
        return new ProGpuDirectXSwapChain(this, descriptor);
    }

    internal void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ProGpuDirectXDevice));
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
    }
}
