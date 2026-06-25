namespace ProGPU.DirectX;

public readonly record struct DxColor(float R, float G, float B, float A)
{
    public static DxColor Transparent { get; } = new(0f, 0f, 0f, 0f);
    public static DxColor Black { get; } = new(0f, 0f, 0f, 1f);
}

public readonly record struct DxViewport(
    float X,
    float Y,
    float Width,
    float Height,
    float MinDepth = 0f,
    float MaxDepth = 1f);

public readonly record struct DxRect(int X, int Y, int Width, int Height);

public sealed record ProGpuDirectXDeviceOptions
{
    public DxFeatureLevel MinimumFeatureLevel { get; init; } = DxFeatureLevel.Direct3D9_3;
    public bool RequireGpuBackedResources { get; init; }
    public bool EnableValidation { get; init; }
    public string Label { get; init; } = "ProGPU DirectX Device";
}

public sealed record DxBufferDescriptor
{
    public required uint SizeInBytes { get; init; }
    public DxBufferUsage Usage { get; init; } = DxBufferUsage.Vertex | DxBufferUsage.CopyDestination;
    public uint StrideInBytes { get; init; }
    public string Label { get; init; } = "DirectXBuffer";
}

public sealed record DxTexture2DDescriptor
{
    public required uint Width { get; init; }
    public required uint Height { get; init; }
    public DxResourceFormat Format { get; init; } = DxResourceFormat.B8G8R8A8Unorm;
    public DxTextureUsage Usage { get; init; } = DxTextureUsage.ShaderResource | DxTextureUsage.RenderTarget | DxTextureUsage.CopyDestination;
    public uint MipLevels { get; init; } = 1;
    public uint ArraySize { get; init; } = 1;
    public uint SampleCount { get; init; } = 1;
    public string Label { get; init; } = "DirectXTexture2D";
}

public sealed record DxSwapChainDescriptor
{
    public required uint Width { get; init; }
    public required uint Height { get; init; }
    public DxResourceFormat Format { get; init; } = DxResourceFormat.B8G8R8A8Unorm;
    public DxPresentMode PresentMode { get; init; } = DxPresentMode.Immediate;
    public string Label { get; init; } = "DirectXSwapChain";
}

public sealed record DxDrawCall(
    DxPrimitiveTopology Topology,
    uint VertexCount,
    uint StartVertexLocation,
    uint InstanceCount,
    uint StartInstanceLocation);

public sealed record DxDrawIndexedCall(
    DxPrimitiveTopology Topology,
    uint IndexCount,
    uint StartIndexLocation,
    int BaseVertexLocation,
    uint InstanceCount,
    uint StartInstanceLocation,
    DxIndexFormat IndexFormat);
