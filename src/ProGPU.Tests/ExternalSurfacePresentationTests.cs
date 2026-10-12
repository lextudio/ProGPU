using ProGPU.Backend;
using Silk.NET.WebGPU;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;
using Xunit;

namespace ProGPU.Tests;

[Collection(WgpuContextLossCollection.Name)]
public sealed unsafe class ExternalSurfacePresentationTests
{
    [Fact]
    public void LegacyProviderKeepsItsOwnPolicyAndDoesNotReportAnInventedMode()
    {
        var api = new LegacySurfaceApi();
        using var context = CreateContext(api);
        context.AttachExternalNativePresentationSurface((Surface*)3, TextureFormat.Bgra8Unorm, 640, 480);
        Assert.Null(context.ConfiguredPresentMode);
        context.VSync = true;
        Assert.Equal(2, api.ConfigureCount);
        Assert.Equal((640u, 480u), api.Size);
        Assert.Null(context.ConfiguredPresentMode);
        context.DetachExternalNativePresentationSurface();
        Assert.Equal(1, api.UnconfigureCount);
        Assert.Equal(1, api.ReleaseCount);
        Assert.Null(context.ConfiguredPresentMode);
    }

    [Fact]
    public void ActualProviderSelectionSurvivesResizeAndChangesWhenVsyncChanges()
    {
        var api = new PolicySurfaceApi();
        using var context = CreateContext(api);
        context.AttachExternalNativePresentationSurface((Surface*)3, TextureFormat.Bgra8Unorm, 640, 480);
        Assert.Equal(PresentMode.Immediate, context.ConfiguredPresentMode);
        context.VSync = true;
        Assert.Equal(PresentMode.Fifo, context.ConfiguredPresentMode);
        Assert.True(context.TryReconfigureIfNeeded(800, 600, waitForNativeCompletion: false));
        Assert.Equal(PresentMode.Fifo, context.ConfiguredPresentMode);
        context.VSync = false;
        Assert.Equal(PresentMode.Immediate, context.ConfiguredPresentMode);
        Assert.Equal(new[] { false, true, true, false }, api.Requests);
        Assert.Equal((800u, 600u), api.Size);
        Assert.Equal(0, api.ConfigureCount);
        Assert.Equal(4UL, context.SurfaceConfigurationCount);
        context.DetachExternalNativePresentationSurface();
        Assert.Null(context.ConfiguredPresentMode);
        Assert.Equal(1, api.UnconfigureCount);
        Assert.Equal(1, api.ReleaseCount);
    }

    [Fact]
    public void FifoFallbackIsReportedAndFailureCannotPublishTheOldMode()
    {
        var api = new PolicySurfaceApi { UncappedMode = PresentMode.Fifo };
        using var context = CreateContext(api);
        context.AttachExternalNativePresentationSurface((Surface*)3, TextureFormat.Bgra8Unorm, 640, 480);
        Assert.False(context.VSync);
        Assert.Equal(PresentMode.Fifo, context.ConfiguredPresentMode);
        api.Fail = true;
        Assert.Throws<InvalidOperationException>(() => context.TryConfigureSwapChain(800, 600));
        Assert.Null(context.ConfiguredPresentMode);
        Assert.Equal(1UL, context.SurfaceConfigurationCount);
        api.Fail = false;
        Assert.True(context.TryConfigureSwapChain(800, 600));
        Assert.Equal(PresentMode.Fifo, context.ConfiguredPresentMode);
        context.InvalidateSurfaceConfiguration();
        Assert.Null(context.ConfiguredPresentMode);
        Assert.True(context.TryConfigureSwapChain(800, 600));
        context.ReportDeviceLost(DeviceLostReason.Unknown, "test device loss");
        Assert.Null(context.ConfiguredPresentMode);
    }

    private static WgpuContext CreateContext(LegacySurfaceApi api)
    {
        var context = new WgpuContext();
        context.InitializeExternalNativeDevice(api, new Lifetime(), (Device*)1, (Queue*)2, TextureFormat.Bgra8Unorm);
        api.Context = context;
        return context;
    }

    private sealed class Lifetime : IWebGpuExternalDeviceLifetime
    {
        public void Poll(bool wait) { }
        public void Dispose() { }
    }

    private sealed class PolicySurfaceApi : LegacySurfaceApi, IWebGpuExternalSurfacePresentationApi
    {
        public readonly List<bool> Requests = new();
        public PresentMode UncappedMode = PresentMode.Immediate;
        public bool Fail;

        public PresentMode ConfigureExternalSurface(Surface* surface, uint width, uint height, bool vsync)
        {
            Validate(surface);
            if (Fail) throw new InvalidOperationException("test configuration failure");
            Requests.Add(vsync);
            Size = (width, height);
            return vsync ? PresentMode.Fifo : UncappedMode;
        }
    }

    // Opaque test-owned handles never reach a native library. All calls other
    // than the expected surface operations fail explicitly.
    private class LegacySurfaceApi : IWebGpuApi, IWebGpuExternalSurfaceApi
    {
        public WgpuContext Context = null!;
        public int ConfigureCount, UnconfigureCount, ReleaseCount;
        public (uint Width, uint Height) Size;
        protected void Validate(Surface* surface)
        {
            Assert.Equal((nuint)3, (nuint)surface);
            Assert.True(Monitor.IsEntered(Context.RenderLock));
        }
        public void ConfigureExternalSurface(Surface* surface, uint width, uint height)
        {
            Validate(surface);
            ConfigureCount++;
            Size = (width, height);
        }
        public void UnconfigureExternalSurface(Surface* surface)
        {
            Validate(surface);
            UnconfigureCount++;
        }
        public BindGroup* DeviceCreateBindGroup(Device* device, BindGroupDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public BindGroupLayout* DeviceCreateBindGroupLayout(Device* device, BindGroupLayoutDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public WgpuBuffer* DeviceCreateBuffer(Device* device, BufferDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public CommandEncoder* DeviceCreateCommandEncoder(Device* device, CommandEncoderDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public ComputePipeline* DeviceCreateComputePipeline(Device* device, ComputePipelineDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public PipelineLayout* DeviceCreatePipelineLayout(Device* device, PipelineLayoutDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public RenderPipeline* DeviceCreateRenderPipeline(Device* device, RenderPipelineDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public Sampler* DeviceCreateSampler(Device* device, SamplerDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public ShaderModule* DeviceCreateShaderModule(Device* device, ShaderModuleDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public Texture* DeviceCreateTexture(Device* device, TextureDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public TextureView* TextureCreateView(Texture* texture, TextureViewDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public BindGroupLayout* ComputePipelineGetBindGroupLayout(ComputePipeline* computePipeline, uint groupIndex) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public BindGroupLayout* RenderPipelineGetBindGroupLayout(RenderPipeline* renderPipeline, uint groupIndex) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public ComputePassEncoder* CommandEncoderBeginComputePass(CommandEncoder* commandEncoder, ComputePassDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public RenderPassEncoder* CommandEncoderBeginRenderPass(CommandEncoder* commandEncoder, RenderPassDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void CommandEncoderCopyBufferToBuffer(CommandEncoder* commandEncoder, WgpuBuffer* source, ulong sourceOffset, WgpuBuffer* destination, ulong destinationOffset, ulong size) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void CommandEncoderCopyBufferToTexture(CommandEncoder* commandEncoder, ImageCopyBuffer* source, ImageCopyTexture* destination, Extent3D* copySize) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void CommandEncoderCopyTextureToBuffer(CommandEncoder* commandEncoder, ImageCopyTexture* source, ImageCopyBuffer* destination, Extent3D* copySize) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void CommandEncoderCopyTextureToTexture(CommandEncoder* commandEncoder, ImageCopyTexture* source, ImageCopyTexture* destination, Extent3D* copySize) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public CommandBuffer* CommandEncoderFinish(CommandEncoder* commandEncoder, CommandBufferDescriptor* descriptor) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void ComputePassEncoderSetPipeline(ComputePassEncoder* pass, ComputePipeline* pipeline) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void ComputePassEncoderSetBindGroup(ComputePassEncoder* pass, uint groupIndex, BindGroup* group, nuint dynamicOffsetCount, uint* dynamicOffsets) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void ComputePassEncoderDispatchWorkgroups(ComputePassEncoder* pass, uint x, uint y, uint z) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void ComputePassEncoderDispatchWorkgroupsIndirect(ComputePassEncoder* pass, WgpuBuffer* indirectBuffer, ulong indirectOffset) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void ComputePassEncoderEnd(ComputePassEncoder* pass) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPassEncoderSetPipeline(RenderPassEncoder* pass, RenderPipeline* pipeline) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPassEncoderSetBindGroup(RenderPassEncoder* pass, uint groupIndex, BindGroup* group, nuint dynamicOffsetCount, uint* dynamicOffsets) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPassEncoderSetVertexBuffer(RenderPassEncoder* pass, uint slot, WgpuBuffer* buffer, ulong offset, ulong size) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPassEncoderSetIndexBuffer(RenderPassEncoder* pass, WgpuBuffer* buffer, IndexFormat format, ulong offset, ulong size) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPassEncoderSetScissorRect(RenderPassEncoder* pass, uint x, uint y, uint width, uint height) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPassEncoderSetStencilReference(RenderPassEncoder* pass, uint reference) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPassEncoderSetViewport(RenderPassEncoder* pass, float x, float y, float width, float height, float minDepth, float maxDepth) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPassEncoderDraw(RenderPassEncoder* pass, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPassEncoderDrawIndexed(RenderPassEncoder* pass, uint indexCount, uint instanceCount, uint firstIndex, int baseVertex, uint firstInstance) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPassEncoderEnd(RenderPassEncoder* pass) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void QueueWriteBuffer(Queue* queue, WgpuBuffer* buffer, ulong bufferOffset, void* data, nuint size) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void QueueWriteTexture(Queue* queue, ImageCopyTexture* destination, void* data, nuint dataSize, TextureDataLayout* dataLayout, Extent3D* writeSize) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void QueueSubmit(Queue* queue, nuint commandCount, CommandBuffer** commands) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void BufferMapAsync(WgpuBuffer* buffer, MapMode mode, nuint offset, nuint size, PfnBufferMapCallback callback, void* userData) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public Task<BufferMapAsyncStatus> BufferMapAsyncTask(WgpuBuffer* buffer, MapMode mode, nuint offset, nuint size) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void* BufferGetMappedRange(WgpuBuffer* buffer, nuint offset, nuint size) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void* BufferGetConstMappedRange(WgpuBuffer* buffer, nuint offset, nuint size) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void BufferUnmap(WgpuBuffer* buffer) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void BufferDestroy(WgpuBuffer* buffer) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void SurfaceGetCurrentTexture(Surface* surface, SurfaceTexture* surfaceTexture) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void SurfacePresent(Surface* surface) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void SurfaceRelease(Surface* surface) { Validate(surface); ReleaseCount++; }
        public void BindGroupRelease(BindGroup* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void BindGroupLayoutRelease(BindGroupLayout* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void BufferRelease(WgpuBuffer* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void CommandBufferRelease(CommandBuffer* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void CommandEncoderRelease(CommandEncoder* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void ComputePassEncoderRelease(ComputePassEncoder* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void ComputePipelineRelease(ComputePipeline* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void PipelineLayoutRelease(PipelineLayout* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPassEncoderRelease(RenderPassEncoder* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void RenderPipelineRelease(RenderPipeline* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void SamplerRelease(Sampler* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void ShaderModuleRelease(ShaderModule* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void TextureDestroy(Texture* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void TextureRelease(Texture* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
        public void TextureViewRelease(TextureView* value) => throw new NotSupportedException("Unexpected GPU operation in presentation policy test.");
    }
}
