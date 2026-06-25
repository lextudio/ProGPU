using ProGPU.Backend;

namespace ProGPU.DirectX;

public abstract class ProGpuDirectXResource : IDisposable
{
    private bool _isDisposed;

    protected ProGpuDirectXResource(ProGpuDirectXDevice device, string label)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        Label = label;
    }

    public ProGpuDirectXDevice Device { get; }

    public string Label { get; }

    public bool IsDisposed => _isDisposed;

    protected void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    protected virtual void DisposeCore()
    {
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        DisposeCore();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}

public sealed class ProGpuDirectXBuffer : ProGpuDirectXResource
{
    private readonly GpuBuffer? _backendBuffer;

    internal ProGpuDirectXBuffer(ProGpuDirectXDevice device, DxBufferDescriptor descriptor)
        : base(device, descriptor.Label)
    {
        if (descriptor.SizeInBytes == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "DirectX buffers must have a non-zero size.");
        }

        Descriptor = descriptor;
        if (device.Context is { } context && device.IsGpuBacked)
        {
            _backendBuffer = new GpuBuffer(
                context,
                descriptor.SizeInBytes,
                ProGpuDirectXFormatConverter.ToBufferUsage(descriptor.Usage),
                descriptor.Label);
        }
    }

    public DxBufferDescriptor Descriptor { get; }

    public GpuBuffer? BackendBuffer => _backendBuffer;

    public uint LastWriteSizeInBytes { get; private set; }

    public unsafe void Write<T>(ReadOnlySpan<T> data, uint offsetBytes = 0) where T : unmanaged
    {
        ThrowIfDisposed();
        var dataSize = checked((uint)(data.Length * sizeof(T)));
        if (offsetBytes + dataSize > Descriptor.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "Buffer write exceeds the DirectX buffer bounds.");
        }

        _backendBuffer?.Write(data, offsetBytes);
        LastWriteSizeInBytes = dataSize;
    }

    protected override void DisposeCore()
    {
        _backendBuffer?.Dispose();
    }
}

public sealed class ProGpuDirectXTexture2D : ProGpuDirectXResource
{
    private GpuTexture? _backendTexture;

    internal ProGpuDirectXTexture2D(ProGpuDirectXDevice device, DxTexture2DDescriptor descriptor)
        : base(device, descriptor.Label)
    {
        ValidateDescriptor(descriptor);
        Descriptor = descriptor;
        AllocateBackendTexture();
    }

    public DxTexture2DDescriptor Descriptor { get; private set; }

    public GpuTexture? BackendTexture => _backendTexture;

    public uint Width => Descriptor.Width;

    public uint Height => Descriptor.Height;

    public uint Generation { get; private set; }

    public unsafe void WritePixels<T>(ReadOnlySpan<T> pixels) where T : unmanaged
    {
        ThrowIfDisposed();
        _backendTexture?.WritePixels(pixels);
        Generation++;
    }

    public void Resize(uint width, uint height)
    {
        ThrowIfDisposed();
        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (Descriptor.Width == width && Descriptor.Height == height)
        {
            return;
        }

        Descriptor = Descriptor with { Width = width, Height = height };
        if (_backendTexture != null)
        {
            _backendTexture.Resize(width, height);
        }

        Generation++;
    }

    private void AllocateBackendTexture()
    {
        if (Device.Context is not { } context || !Device.IsGpuBacked)
        {
            return;
        }

        _backendTexture = new GpuTexture(
            context,
            Descriptor.Width,
            Descriptor.Height,
            ProGpuDirectXFormatConverter.ToTextureFormat(Descriptor.Format),
            ProGpuDirectXFormatConverter.ToTextureUsage(Descriptor.Usage),
            Descriptor.Label,
            Descriptor.SampleCount,
            ProGpuDirectXFormatConverter.ToTextureAlphaMode(Descriptor.Format));
    }

    private static void ValidateDescriptor(DxTexture2DDescriptor descriptor)
    {
        if (descriptor.Width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "DirectX textures must have a non-zero width.");
        }

        if (descriptor.Height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "DirectX textures must have a non-zero height.");
        }

        if (descriptor.MipLevels == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "DirectX textures must have at least one mip level.");
        }

        if (descriptor.ArraySize == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "DirectX textures must have at least one array slice.");
        }

        if (descriptor.SampleCount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(descriptor), "DirectX textures must have at least one sample.");
        }
    }

    protected override void DisposeCore()
    {
        _backendTexture?.Dispose();
        _backendTexture = null;
    }
}

public sealed class ProGpuDirectXSwapChain : IDisposable
{
    private readonly ProGpuDirectXDevice _device;
    private ProGpuDirectXTexture2D _backBuffer;
    private bool _isDisposed;

    internal ProGpuDirectXSwapChain(ProGpuDirectXDevice device, DxSwapChainDescriptor descriptor)
    {
        _device = device;
        Descriptor = descriptor;
        _backBuffer = CreateBackBuffer(device, descriptor);
    }

    public DxSwapChainDescriptor Descriptor { get; private set; }

    public ProGpuDirectXTexture2D BackBuffer => _backBuffer;

    public ulong PresentCount { get; private set; }

    public ProGpuDirectXTexture2D AcquireBackBuffer()
    {
        ThrowIfDisposed();
        return _backBuffer;
    }

    public void Resize(uint width, uint height)
    {
        ThrowIfDisposed();
        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Descriptor = Descriptor with { Width = width, Height = height };
        _backBuffer.Resize(width, height);
    }

    public void Present()
    {
        ThrowIfDisposed();
        PresentCount++;
    }

    private static ProGpuDirectXTexture2D CreateBackBuffer(ProGpuDirectXDevice device, DxSwapChainDescriptor descriptor)
    {
        return device.CreateTexture2D(new DxTexture2DDescriptor
        {
            Width = descriptor.Width,
            Height = descriptor.Height,
            Format = descriptor.Format,
            Usage = DxTextureUsage.RenderTarget | DxTextureUsage.Present | DxTextureUsage.CopyDestination,
            Label = descriptor.Label + ".BackBuffer"
        });
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ProGpuDirectXSwapChain));
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _backBuffer.Dispose();
        _isDisposed = true;
    }
}
