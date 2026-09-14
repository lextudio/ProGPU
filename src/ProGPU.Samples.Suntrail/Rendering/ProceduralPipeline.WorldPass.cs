using System.Numerics;
using ProGPU.Backend;
using ProGPU.GameEngine.Rendering;
using ProGPU.Scene;
using Silk.NET.WebGPU;

namespace ProGPU.Samples.Suntrail.Rendering;

public sealed unsafe partial class ProceduralPipeline
{
    // Measurement opt-in until all-world image and device gates are completed.
    public bool EnableWorldPass { get; set; }
    public long WorldRenderCount { get; private set; }
    public long WorldResidentBytes => _worldTarget?.ResidentBytes ?? 0;
    public bool WorldPassReady => _worldReady;
    private SceneRenderTarget? _worldTarget;
    private GpuBuffer? _worldQuad;
    private BindGroupLayout* _worldStorageLayout, _worldSampleLayout;
    private PipelineLayout* _worldLayout, _worldResolveLayout;
    private BindGroup* _worldStorageGroup, _worldSampleGroup;
    private readonly nint[] _worldPipelines = new nint[(Entries.Length + 2) * 2], _worldResolvePipelines = new nint[2];
    private uint _worldPipelineSamples;
    private TextureFormat _worldPipelineFormat;
    private WorldKey _worldKey;
    private Vector2 _worldQuadSize;
    private bool _worldReady, _worldRendered;
    private readonly record struct WorldKey(ProceduralBatch Batch, uint BatchGeneration, uint PageGeneration,
        uint TargetGeneration, bool EarlyCoverage, bool WorldShaders, bool Specialized, bool SharedInstances);

    private void PrepareWorldPass(Compositor compositor, ProceduralBatch batch, bool offscreen)
    {
        if (!EnableWorldPass || !_pagesPrepared || _pageCount == 0 || _transform != Matrix4x4.Identity)
        { ReleaseUnusedWorldPass(); return; }
        float pixelWidth = batch.Size.X * compositor.CurrentDpiScale;
        float pixelHeight = batch.Size.Y * compositor.CurrentDpiScale;
        if (!float.IsFinite(pixelWidth) || !float.IsFinite(pixelHeight) || pixelWidth < 1 || pixelHeight < 1 || pixelWidth > 8192 || pixelHeight > 8192)
        { ReleaseUnusedWorldPass(); return; }
        uint width = (uint)MathF.Round(pixelWidth), height = (uint)MathF.Round(pixelHeight);
        if (MathF.Abs(pixelWidth - width) > .001f || MathF.Abs(pixelHeight - height) > .001f)
        { ReleaseUnusedWorldPass(); return; }
        var format = compositor.RenderFormat;
        uint samples = offscreen ? 1u : compositor.Options.PrimarySampleCount;
        if (samples is not (1 or 4) || format is not (TextureFormat.Bgra8Unorm or TextureFormat.Bgra8UnormSrgb or TextureFormat.Rgba8Unorm or TextureFormat.Rgba8UnormSrgb))
        { ReleaseUnusedWorldPass(); return; }
        EnsureWorldResources();
        bool resized = _worldTarget!.Color is not { } color || color.Width != width || color.Height != height || color.Format != format || _worldTarget.SampleCount != samples;
        if (resized) ReleaseWorldSampleGroup();
        if (!_worldTarget.TryEnsure(width, height, samples, format)) { _worldRendered = false; return; }
        if (_worldPipelineSamples != samples || _worldPipelineFormat != format)
        {
            Array.Clear(_worldPipelines); Array.Clear(_worldResolvePipelines);
            _worldPipelineSamples = samples; _worldPipelineFormat = format;
        }
        if (_worldSampleGroup == null)
        {
            var binding = new BindGroupEntry { Binding = 0, TextureView = _worldTarget.Color!.ViewPtr };
            var descriptor = new BindGroupDescriptor { Layout = _worldSampleLayout, EntryCount = 1, Entries = &binding };
            _worldSampleGroup = _context!.Api.DeviceCreateBindGroup(_context.Device, &descriptor);
            _worldRendered = false;
        }
        var key = new WorldKey(batch, batch.Generation, _pageUploadGeneration, _worldTarget.Generation,
            _earlyCoverage, EnableWorldShaders, EnableSpecializedShaders, _sharedInstancesPrepared);
        if (!_worldRendered || key != _worldKey)
        {
            UpdateFrameData(batch);
            if (resized || !_worldRendered || _worldQuadSize != batch.Size)
            {
                _worldQuad!.WriteSingle(new ProceduralSprite(new(0, 0, batch.Size.X, batch.Size.Y), Vector4.One, default));
                UploadedBytes += 48; _worldQuadSize = batch.Size;
            }
            EncodeWorld(compositor, batch);
            _worldKey = key; _worldRendered = true;
        }
        _worldReady = true;
    }

    private void EnsureWorldResources()
    {
        if (_worldTarget is not null) return;
        var context = _context!; var api = context.Api;
        _worldTarget = new(context, 192L * 1024 * 1024);
        _worldQuad = new(context, 48, BufferUsage.Vertex | BufferUsage.CopyDst, "Suntrail scene resolve quad");
        var storage = new BindGroupLayoutEntry { Binding = 0, Visibility = ShaderStage.Vertex,
            Buffer = new() { Type = BufferBindingType.ReadOnlyStorage, MinBindingSize = 96 } };
        var storageDescriptor = new BindGroupLayoutDescriptor { EntryCount = 1, Entries = &storage };
        _worldStorageLayout = api.DeviceCreateBindGroupLayout(context.Device, &storageDescriptor);
        var pageBinding = new BindGroupEntry { Binding = 0, Buffer = _pageBuffer!.BufferPtr, Size = _pageBuffer.Size };
        var group = new BindGroupDescriptor { Layout = _worldStorageLayout, EntryCount = 1, Entries = &pageBinding };
        _worldStorageGroup = api.DeviceCreateBindGroup(context.Device, &group);
        var layouts = stackalloc BindGroupLayout*[3]; layouts[0] = _layout; layouts[1] = _materialSampleLayout; layouts[2] = _worldStorageLayout;
        var pipeline = new PipelineLayoutDescriptor { BindGroupLayoutCount = 3, BindGroupLayouts = layouts };
        _worldLayout = api.DeviceCreatePipelineLayout(context.Device, &pipeline);
        var texture = new BindGroupLayoutEntry { Binding = 0, Visibility = ShaderStage.Fragment,
            Texture = new() { SampleType = TextureSampleType.Float, ViewDimension = TextureViewDimension.Dimension2D } };
        var sampleDescriptor = new BindGroupLayoutDescriptor { EntryCount = 1, Entries = &texture };
        _worldSampleLayout = api.DeviceCreateBindGroupLayout(context.Device, &sampleDescriptor);
        layouts[1] = _worldSampleLayout;
        pipeline.BindGroupLayoutCount = 2;
        _worldResolveLayout = api.DeviceCreatePipelineLayout(context.Device, &pipeline);
    }

    private void EncodeWorld(Compositor compositor, ProceduralBatch batch)
    {
        var context = _context!; var api = context.Api; var target = _worldTarget!;
        var encoder = api.DeviceCreateCommandEncoder(context.Device, null);
        CommandBuffer* commands = null; RenderPassEncoder* pass = null;
        try
        {
            var color = new RenderPassColorAttachment { View = (target.MultisampleColor ?? target.Color)!.ViewPtr,
                ResolveTarget = target.MultisampleColor is null ? null : target.Color!.ViewPtr,
                LoadOp = LoadOp.Clear, StoreOp = target.MultisampleColor is null ? StoreOp.Store : StoreOp.Discard,
                ClearValue = new(0, 0, 0, 0), DepthSlice = uint.MaxValue };
            var depth = new RenderPassDepthStencilAttachment { View = target.Depth!.ViewPtr,
                DepthLoadOp = LoadOp.Clear, DepthStoreOp = StoreOp.Discard, DepthClearValue = 1,
                DepthReadOnly = false, StencilReadOnly = true };
            var descriptor = new RenderPassDescriptor { ColorAttachmentCount = 1, ColorAttachments = &color, DepthStencilAttachment = &depth };
            pass = api.CommandEncoderBeginRenderPass(encoder, &descriptor);
            api.RenderPassEncoderSetBindGroup(pass, 0, _group, 0, null);
            api.RenderPassEncoderSetBindGroup(pass, 1, _materialSampleGroup, 0, null);
            api.RenderPassEncoderSetBindGroup(pass, 2, _sharedInstancesPrepared ? _sharedStorageGroup : _worldStorageGroup, 0, null);
            // Vertex pulling visits the existing contiguous page buffer in reverse.
            // No reversed CPU copy, index upload, per-page draw or sorting allocation.
            api.RenderPassEncoderSetPipeline(pass, GetWorldPipeline(Entries.Length + 1));
            api.RenderPassEncoderDraw(pass, 6, (uint)_pageCount, 0, (uint)(PageInstanceCapacity - _pageCount)); Draws++;
            if (!_sharedInstancesPrepared) api.RenderPassEncoderSetVertexBuffer(pass, 0, _pageBuffer!.BufferPtr, 0, _pageBuffer.Size);
            for (int first = 0; first < _pageCount;)
            {
                int variant = PageVariant(first, batch), end = first + 1;
                while (end < _pageCount && PageVariant(end, batch) == variant) end++;
                api.RenderPassEncoderSetPipeline(pass, GetWorldPipeline(variant));
                api.RenderPassEncoderDraw(pass, 6, (uint)(end - first), 0, (uint)first); Draws++; first = end;
            }
            api.RenderPassEncoderEnd(pass); api.RenderPassEncoderRelease(pass); pass = null;
            commands = api.CommandEncoderFinish(encoder, null);
            context.Submit(1, &commands); WorldRenderCount++;
        }
        finally
        {
            if (pass != null) { api.RenderPassEncoderEnd(pass); api.RenderPassEncoderRelease(pass); }
            if (commands != null) api.CommandBufferRelease(commands);
            api.CommandEncoderRelease(encoder);
        }
    }

    private RenderPipeline* GetWorldPipeline(int variant)
    {
        int slot = variant + (_sharedInstancesPrepared ? Entries.Length + 2 : 0);
        if (_worldPipelines[slot] != 0) return (RenderPipeline*)_worldPipelines[slot];
        bool opaque = variant == Entries.Length + 1;
        var module = _cache!.GetOrCreateShader("Suntrail.Art.v1", Shader, "Suntrail procedural artwork");
        var attributes = stackalloc VertexAttribute[6];
        for (uint i = 0; i < 6; i++) attributes[i] = new() { ShaderLocation = i, Offset = i * 16, Format = VertexFormat.Float32x4 };
        Span<VertexBufferLayout> buffers = stackalloc VertexBufferLayout[1];
        buffers[0] = new() { ArrayStride = 96, StepMode = VertexStepMode.Instance, AttributeCount = 6, Attributes = attributes };
        var result = _cache.GetOrCreateRenderPipeline($"Art.world.{_worldPipelineFormat}.{_worldPipelineSamples}.{variant}.{_sharedInstancesPrepared}", module,
            opaque || _sharedInstancesPrepared ? ReadOnlySpan<VertexBufferLayout>.Empty : buffers,
            vertexEntry: _sharedInstancesPrepared ? (opaque ? "vs_world_shared_opaque" : "vs_world_shared_page") : (opaque ? "vs_world_opaque" : "vs_world_page"),
            fragmentEntry: opaque ? "fs_world_opaque" : variant == Entries.Length ? "fs_world_translucent" : Entries[variant],
            targetFormat: _worldPipelineFormat, sampleCount: _worldPipelineSamples, enableBlend: !opaque,
            enableDepthStencil: true, depthFormat: TextureFormat.Depth32float, depthWriteEnabled: opaque,
            depthCompare: CompareFunction.Less, pipelineLayout: _sharedInstancesPrepared ? _sharedReplayLayout : _worldLayout, sourceAlphaMode: GpuTextureAlphaMode.Premultiplied);
        _worldPipelines[slot] = (nint)result; return result;
    }

    private void RenderWorldResolve(Compositor compositor, RenderPassEncoder* pass, bool offscreen)
    {
        int slot = offscreen ? 1 : 0;
        if (_worldResolvePipelines[slot] == 0)
        {
            var module = _cache!.GetOrCreateShader("Suntrail.Art.v1", Shader, "Suntrail procedural artwork");
            var attributes = stackalloc VertexAttribute[3];
            for (uint i = 0; i < 3; i++) attributes[i] = new() { ShaderLocation = i, Offset = i * 16, Format = VertexFormat.Float32x4 };
            Span<VertexBufferLayout> buffers = stackalloc VertexBufferLayout[1];
            buffers[0] = new() { ArrayStride = 48, StepMode = VertexStepMode.Instance, AttributeCount = 3, Attributes = attributes };
            _worldResolvePipelines[slot] = (nint)_cache.GetOrCreateRenderPipeline($"Art.world.resolve.{compositor.RenderFormat}.{offscreen}.{compositor.Options.PrimarySampleCount}", module, buffers,
                fragmentEntry: "fs_world_resolve", targetFormat: compositor.RenderFormat,
                sampleCount: offscreen ? 1u : compositor.Options.PrimarySampleCount,
                pipelineLayout: _worldResolveLayout, sourceAlphaMode: GpuTextureAlphaMode.Premultiplied);
        }
        var api = _context!.Api;
        api.RenderPassEncoderSetPipeline(pass, (RenderPipeline*)_worldResolvePipelines[slot]);
        api.RenderPassEncoderSetBindGroup(pass, 0, _group, 0, null);
        api.RenderPassEncoderSetBindGroup(pass, 1, _worldSampleGroup, 0, null);
        api.RenderPassEncoderSetVertexBuffer(pass, 0, _worldQuad!.BufferPtr, 0, 48);
        api.RenderPassEncoderDraw(pass, 6, 1, 0, 0); Draws++;
    }

    private void ReleaseWorldSampleGroup()
    {
        if (_worldSampleGroup != null && _context is { IsDisposed: false } context) context.QueueBindGroupDisposal((nint)_worldSampleGroup);
        _worldSampleGroup = null;
    }
    private void ReleaseUnusedWorldPass()
    {
        ReleaseWorldSampleGroup(); _worldTarget?.Release(); _worldReady = _worldRendered = false;
    }
    private void DisposeWorldPass()
    {
        ReleaseUnusedWorldPass();
        if (_context is { IsDisposed: false } context)
        {
            if (_worldStorageGroup != null) context.QueueBindGroupDisposal((nint)_worldStorageGroup);
            if (_worldLayout != null) context.QueuePipelineLayoutDisposal((nint)_worldLayout);
            if (_worldResolveLayout != null) context.QueuePipelineLayoutDisposal((nint)_worldResolveLayout);
            if (_worldStorageLayout != null) context.QueueBindGroupLayoutDisposal((nint)_worldStorageLayout);
            if (_worldSampleLayout != null) context.QueueBindGroupLayoutDisposal((nint)_worldSampleLayout);
        }
        _worldTarget?.Dispose(); _worldTarget = null; _worldQuad?.Dispose(); _worldQuad = null;
        _worldStorageGroup = null; _worldLayout = _worldResolveLayout = null; _worldStorageLayout = _worldSampleLayout = null;
        Array.Clear(_worldPipelines); Array.Clear(_worldResolvePipelines); _worldKey = default;
    }
}
