using Silk.NET.Core.Native;
using Silk.NET.WebGPU;

namespace ProGPU.DirectX;

public enum ProGpuDirectXCommandKind
{
    SetRenderTargets,
    SetViewport,
    SetScissorRect,
    SetPrimitiveTopology,
    SetVertexBuffer,
    SetIndexBuffer,
    SetConstantBuffer,
    ClearRenderTarget,
    ClearDepthStencil,
    Draw,
    DrawIndexed,
    Present
}

public sealed record ProGpuDirectXCommand
{
    public required ProGpuDirectXCommandKind Kind { get; init; }
    public ProGpuDirectXTexture2D? Texture { get; init; }
    public ProGpuDirectXBuffer? Buffer { get; init; }
    public DxColor Color { get; init; }
    public DxViewport Viewport { get; init; }
    public DxRect Rect { get; init; }
    public DxPrimitiveTopology Topology { get; init; }
    public DxDrawCall? Draw { get; init; }
    public DxDrawIndexedCall? DrawIndexed { get; init; }
}

public sealed unsafe class ProGpuDirectXDeviceContext : IDisposable
{
    private readonly ProGpuDirectXDevice _device;
    private readonly List<ProGpuDirectXCommand> _commands = new();
    private ProGpuDirectXTexture2D? _renderTarget;
    private ProGpuDirectXTexture2D? _depthStencil;
    private DxPrimitiveTopology _topology = DxPrimitiveTopology.TriangleList;
    private bool _isDisposed;

    internal ProGpuDirectXDeviceContext(ProGpuDirectXDevice device)
    {
        _device = device;
    }

    public IReadOnlyList<ProGpuDirectXCommand> Commands => _commands;

    public ProGpuDirectXTexture2D? RenderTarget => _renderTarget;

    public ProGpuDirectXTexture2D? DepthStencil => _depthStencil;

    public DxViewport Viewport { get; private set; }

    public DxRect ScissorRect { get; private set; }

    public void SetRenderTargets(ProGpuDirectXTexture2D? renderTarget, ProGpuDirectXTexture2D? depthStencil = null)
    {
        ThrowIfDisposed();
        _renderTarget = renderTarget;
        _depthStencil = depthStencil;
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.SetRenderTargets,
            Texture = renderTarget
        });
    }

    public void SetViewport(DxViewport viewport)
    {
        ThrowIfDisposed();
        Viewport = viewport;
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.SetViewport,
            Viewport = viewport
        });
    }

    public void SetScissorRect(DxRect rect)
    {
        ThrowIfDisposed();
        ScissorRect = rect;
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.SetScissorRect,
            Rect = rect
        });
    }

    public void SetPrimitiveTopology(DxPrimitiveTopology topology)
    {
        ThrowIfDisposed();
        _topology = topology;
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.SetPrimitiveTopology,
            Topology = topology
        });
    }

    public void SetVertexBuffer(ProGpuDirectXBuffer buffer)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.SetVertexBuffer,
            Buffer = buffer
        });
    }

    public void SetIndexBuffer(ProGpuDirectXBuffer buffer, DxIndexFormat indexFormat = DxIndexFormat.UInt32)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.SetIndexBuffer,
            Buffer = buffer
        });
    }

    public void SetConstantBuffer(uint slot, ProGpuDirectXBuffer buffer)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.SetConstantBuffer,
            Buffer = buffer
        });
    }

    public void ClearRenderTarget(ProGpuDirectXTexture2D renderTarget, DxColor color)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(renderTarget);
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.ClearRenderTarget,
            Texture = renderTarget,
            Color = color
        });
    }

    public void ClearDepthStencil(ProGpuDirectXTexture2D depthStencil, float depth = 1f, byte stencil = 0)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(depthStencil);
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.ClearDepthStencil,
            Texture = depthStencil,
            Color = new DxColor(depth, stencil / 255f, 0f, 0f)
        });
    }

    public void Draw(uint vertexCount, uint startVertexLocation = 0, uint instanceCount = 1, uint startInstanceLocation = 0)
    {
        ThrowIfDisposed();
        var draw = new DxDrawCall(_topology, vertexCount, startVertexLocation, instanceCount, startInstanceLocation);
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.Draw,
            Topology = _topology,
            Draw = draw
        });
    }

    public void DrawIndexed(
        uint indexCount,
        uint startIndexLocation = 0,
        int baseVertexLocation = 0,
        uint instanceCount = 1,
        uint startInstanceLocation = 0,
        DxIndexFormat indexFormat = DxIndexFormat.UInt32)
    {
        ThrowIfDisposed();
        var draw = new DxDrawIndexedCall(
            _topology,
            indexCount,
            startIndexLocation,
            baseVertexLocation,
            instanceCount,
            startInstanceLocation,
            indexFormat);
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.DrawIndexed,
            Topology = _topology,
            DrawIndexed = draw
        });
    }

    public void Present(ProGpuDirectXSwapChain swapChain)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(swapChain);
        swapChain.Present();
        _commands.Add(new ProGpuDirectXCommand
        {
            Kind = ProGpuDirectXCommandKind.Present,
            Texture = swapChain.BackBuffer
        });
    }

    public void ClearRecordedCommands()
    {
        ThrowIfDisposed();
        _commands.Clear();
    }

    public void Flush(bool clearRecordedCommands = true)
    {
        ThrowIfDisposed();
        _device.ThrowIfDisposed();

        if (_device.Context is { } context && _device.IsGpuBacked)
        {
            ExecuteGpuBackedClearCommands(context);
            context.CleanupPendingResources();
        }

        if (clearRecordedCommands)
        {
            _commands.Clear();
        }
    }

    private void ExecuteGpuBackedClearCommands(ProGPU.Backend.WgpuContext context)
    {
        foreach (var command in _commands)
        {
            if (command.Kind != ProGpuDirectXCommandKind.ClearRenderTarget ||
                command.Texture?.BackendTexture is not { IsDisposed: false, ViewPtr: not null } texture)
            {
                continue;
            }

            var labelPtr = SilkMarshal.StringToPtr("ProGPU DirectX Clear Encoder");
            CommandEncoder* encoder = null;
            RenderPassEncoder* pass = null;
            CommandBuffer* commandBuffer = null;
            try
            {
                var encoderDesc = new CommandEncoderDescriptor { Label = (byte*)labelPtr };
                encoder = context.Wgpu.DeviceCreateCommandEncoder(context.Device, &encoderDesc);
                if (encoder == null)
                {
                    continue;
                }

                var clearColor = command.Color;
                var colorAttachment = new RenderPassColorAttachment
                {
                    View = texture.ViewPtr,
                    ResolveTarget = null,
                    LoadOp = LoadOp.Clear,
                    StoreOp = StoreOp.Store,
                    ClearValue = new Silk.NET.WebGPU.Color
                    {
                        R = clearColor.R,
                        G = clearColor.G,
                        B = clearColor.B,
                        A = clearColor.A
                    }
                };

                var passDesc = new RenderPassDescriptor
                {
                    ColorAttachmentCount = 1,
                    ColorAttachments = &colorAttachment
                };

                pass = context.Wgpu.CommandEncoderBeginRenderPass(encoder, &passDesc);
                if (pass != null)
                {
                    context.Wgpu.RenderPassEncoderEnd(pass);
                    context.Wgpu.RenderPassEncoderRelease(pass);
                    pass = null;
                }

                var commandBufferDesc = new CommandBufferDescriptor();
                commandBuffer = context.Wgpu.CommandEncoderFinish(encoder, &commandBufferDesc);
                if (commandBuffer != null)
                {
                    context.Wgpu.QueueSubmit(context.Queue, 1, &commandBuffer);
                }
            }
            finally
            {
                if (commandBuffer != null)
                {
                    context.Wgpu.CommandBufferRelease(commandBuffer);
                }

                if (pass != null)
                {
                    context.Wgpu.RenderPassEncoderRelease(pass);
                }

                if (encoder != null)
                {
                    context.Wgpu.CommandEncoderRelease(encoder);
                }

                SilkMarshal.Free(labelPtr);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ProGpuDirectXDeviceContext));
        }
    }

    public void Dispose()
    {
        _commands.Clear();
        _isDisposed = true;
    }
}
