using ProGPU.Backend;
using Silk.NET.WebGPU;

namespace ProGPU.DirectX;

internal static class ProGpuDirectXFormatConverter
{
    public static TextureFormat ToTextureFormat(DxResourceFormat format)
    {
        return format switch
        {
            DxResourceFormat.R8Unorm => TextureFormat.R8Unorm,
            DxResourceFormat.R8G8B8A8Unorm => TextureFormat.Rgba8Unorm,
            DxResourceFormat.R8G8B8A8UnormSrgb => TextureFormat.Rgba8UnormSrgb,
            DxResourceFormat.B8G8R8A8Unorm => TextureFormat.Bgra8Unorm,
            DxResourceFormat.B8G8R8A8UnormSrgb => TextureFormat.Bgra8UnormSrgb,
            DxResourceFormat.R16Float => TextureFormat.R16float,
            DxResourceFormat.R32Float => TextureFormat.R32float,
            DxResourceFormat.R32G32Float => TextureFormat.RG32float,
            DxResourceFormat.R32G32B32A32Float => TextureFormat.Rgba32float,
            DxResourceFormat.D24UnormS8UInt => TextureFormat.Depth24PlusStencil8,
            DxResourceFormat.D32Float => TextureFormat.Depth32float,
            _ => TextureFormat.Bgra8Unorm
        };
    }

    public static TextureUsage ToTextureUsage(DxTextureUsage usage)
    {
        var result = TextureUsage.None;
        if ((usage & DxTextureUsage.ShaderResource) != 0)
        {
            result |= TextureUsage.TextureBinding;
        }

        if ((usage & (DxTextureUsage.RenderTarget | DxTextureUsage.DepthStencil | DxTextureUsage.Present)) != 0)
        {
            result |= TextureUsage.RenderAttachment;
        }

        if ((usage & DxTextureUsage.UnorderedAccess) != 0)
        {
            result |= TextureUsage.StorageBinding;
        }

        if ((usage & DxTextureUsage.CopySource) != 0)
        {
            result |= TextureUsage.CopySrc;
        }

        if ((usage & DxTextureUsage.CopyDestination) != 0)
        {
            result |= TextureUsage.CopyDst;
        }

        return result == TextureUsage.None
            ? TextureUsage.TextureBinding | TextureUsage.CopyDst
            : result;
    }

    public static BufferUsage ToBufferUsage(DxBufferUsage usage)
    {
        var result = BufferUsage.None;
        if ((usage & DxBufferUsage.Vertex) != 0)
        {
            result |= BufferUsage.Vertex;
        }

        if ((usage & DxBufferUsage.Index) != 0)
        {
            result |= BufferUsage.Index;
        }

        if ((usage & DxBufferUsage.Constant) != 0)
        {
            result |= BufferUsage.Uniform;
        }

        if ((usage & (DxBufferUsage.Structured | DxBufferUsage.ShaderResource | DxBufferUsage.UnorderedAccess)) != 0)
        {
            result |= BufferUsage.Storage;
        }

        if ((usage & DxBufferUsage.CopySource) != 0)
        {
            result |= BufferUsage.CopySrc;
        }

        if ((usage & DxBufferUsage.CopyDestination) != 0)
        {
            result |= BufferUsage.CopyDst;
        }

        return result == BufferUsage.None
            ? BufferUsage.Vertex | BufferUsage.CopyDst
            : result;
    }

    public static PrimitiveTopology ToPrimitiveTopology(DxPrimitiveTopology topology)
    {
        return topology switch
        {
            DxPrimitiveTopology.PointList => PrimitiveTopology.PointList,
            DxPrimitiveTopology.LineList => PrimitiveTopology.LineList,
            DxPrimitiveTopology.LineStrip => PrimitiveTopology.LineStrip,
            DxPrimitiveTopology.TriangleStrip => PrimitiveTopology.TriangleStrip,
            _ => PrimitiveTopology.TriangleList
        };
    }

    public static GpuTextureAlphaMode ToTextureAlphaMode(DxResourceFormat format)
    {
        return format is DxResourceFormat.B8G8R8A8Unorm or DxResourceFormat.R8G8B8A8Unorm
            ? GpuTextureAlphaMode.Premultiplied
            : GpuTextureAlphaMode.Straight;
    }
}
