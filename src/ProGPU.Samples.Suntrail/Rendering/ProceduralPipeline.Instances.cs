using ProGPU.GameEngine.Rendering;
using ProGPU.Scene;
using Silk.NET.WebGPU;

namespace ProGPU.Samples.Suntrail.Rendering;

public sealed unsafe partial class ProceduralPipeline
{
    // Separate measurement switch; retain the validated expanded stream by default.
    public bool EnableSharedInstances { get; set; }
    // Opt-in camera projection and static/dynamic packing, independently measurable.
    public bool EnableSceneInstances { get; set; }
    public int StaticSourceInstances { get; private set; }
    public long SharedSourceUploadBytes => _sourceStream?.UploadedBytes ?? 0;
    public long SharedPageUploadBytes => _referenceStream?.UploadedBytes ?? 0;
    public long SharedUploadCalls => (_sourceStream?.WriteCount ?? 0) + (_referenceStream?.WriteCount ?? 0);
    private bool _sharedInstancesPrepared;
    private MaterialSourceInstance[]? _sourceInstances;
    private MaterialPageReference[]? _pageReferences;
    private uint[]? _pageSourceIndices;
    private uint[]? _sourceLookup;
    private RetainedGpuBuffer<MaterialSourceInstance>? _sourceStream;
    private RetainedGpuBuffer<MaterialPageReference>? _referenceStream;
    private BindGroupLayout* _sharedStorageLayout;
    private BindGroup* _sharedStorageGroup;
    private PipelineLayout* _sharedReplayLayout;
    private readonly nint[] _sharedOnscreen = new nint[Entries.Length + 1], _sharedOffscreen = new nint[Entries.Length + 1];

    private void PrepareSharedSources(ProceduralBatch batch)
    {
        EnsureSharedResources();
        int next = 0; StaticSourceInstances = 0;
        // Reorder only the source table. The page list retains painter order and
        // references this table explicitly, including dynamic/transparent sprites.
        for (int group = 0; group < (EnableSceneInstances ? 2 : 1); group++)
        {
            for (int i = 0; i < batch.Count; i++)
            {
                ref readonly var sprite = ref batch.Sprites[i];
                ref readonly var placement = ref batch.Placements[i];
                if (EnableSceneInstances && placement.Dynamic != (group == 1)) continue;
                var size = new System.Numerics.Vector2(sprite.Bounds.Z, sprite.Bounds.W) / batch.Scene.W;
                bool world = EnableSceneInstances && placement.IsWorldSpace;
                // Negative X camera factor tags an already-projected screen record.
                // Suntrail uses only nonnegative factors for world/parallax records.
                _sourceInstances![next] = new(world ? placement.Bounds : sprite.Bounds, sprite.Color, sprite.Material,
                    new(size, world ? placement.CameraFactor.X : -1, world ? placement.CameraFactor.Y : 0));
                _sourceLookup![i] = (uint)next++;
            }
            if (EnableSceneInstances && group == 0) StaticSourceInstances = next;
        }
    }

    private void UploadSharedInstances(ProceduralBatch batch)
    {
        EnsureSharedResources();
        for (int i = 0; i < _pageCount; i++)
        {
            ref readonly var page = ref _pageInstances[i];
            _pageReferences![i] = new(page.SourceRect, page.AtlasRect, _pageSourceIndices![i], page.SourceSize.Z > .5f ? 1u : 0u);
        }
        long before = SharedSourceUploadBytes + SharedPageUploadBytes;
        bool sourcesChanged = _sourceStream!.Update(_sourceInstances!.AsSpan(0, batch.Count));
        bool pagesChanged = _referenceStream!.Update(_pageReferences!.AsSpan(0, _pageCount));
        UploadedBytes += SharedSourceUploadBytes + SharedPageUploadBytes - before;
        if (sourcesChanged || pagesChanged) _pageUploadGeneration++;
        _sharedInstancesPrepared = true;
    }

    private void EnsureSharedResources()
    {
        if (_sourceStream is not null) return;
        var context = _context!; var api = context.Api;
        _sourceInstances = new MaterialSourceInstance[ProceduralBatch.Capacity];
        _pageReferences = new MaterialPageReference[PageInstanceCapacity];
        _pageSourceIndices = new uint[PageInstanceCapacity];
        _sourceLookup = new uint[ProceduralBatch.Capacity];
        _sourceStream = new(context, ProceduralBatch.Capacity, BufferUsage.Storage, "Suntrail shared source instances");
        _referenceStream = new(context, PageInstanceCapacity, BufferUsage.Storage, "Suntrail retained page references");
        var entries = stackalloc BindGroupLayoutEntry[2];
        // Binding zero is reserved for the existing expanded world-page stream.
        entries[0] = new() { Binding = 1, Visibility = ShaderStage.Vertex,
            Buffer = new() { Type = BufferBindingType.ReadOnlyStorage, MinBindingSize = 48 } };
        entries[1] = new() { Binding = 2, Visibility = ShaderStage.Vertex,
            Buffer = new() { Type = BufferBindingType.ReadOnlyStorage, MinBindingSize = 64 } };
        var descriptor = new BindGroupLayoutDescriptor { EntryCount = 2, Entries = entries };
        _sharedStorageLayout = api.DeviceCreateBindGroupLayout(context.Device, &descriptor);
        var bindings = stackalloc BindGroupEntry[2];
        bindings[0] = new() { Binding = 1, Buffer = _referenceStream.Buffer.BufferPtr, Size = _referenceStream.Buffer.Size };
        bindings[1] = new() { Binding = 2, Buffer = _sourceStream.Buffer.BufferPtr, Size = _sourceStream.Buffer.Size };
        var group = new BindGroupDescriptor { Layout = _sharedStorageLayout, EntryCount = 2, Entries = bindings };
        _sharedStorageGroup = api.DeviceCreateBindGroup(context.Device, &group);
        var layouts = stackalloc BindGroupLayout*[3]; layouts[0] = _layout; layouts[1] = _materialSampleLayout; layouts[2] = _sharedStorageLayout;
        var pipeline = new PipelineLayoutDescriptor { BindGroupLayoutCount = 3, BindGroupLayouts = layouts };
        _sharedReplayLayout = api.DeviceCreatePipelineLayout(context.Device, &pipeline);
    }

    private RenderPipeline* GetSharedPagePipeline(Compositor compositor, bool offscreen, int variant)
    {
        var pipelines = offscreen ? _sharedOffscreen : _sharedOnscreen;
        if (pipelines[variant] != 0) return (RenderPipeline*)pipelines[variant];
        var module = _cache!.GetOrCreateShader("Suntrail.Art.v1", Shader, "Suntrail procedural artwork");
        var pipeline = _cache.GetOrCreateRenderPipeline($"Art.shared.{offscreen}.{variant}", module, ReadOnlySpan<VertexBufferLayout>.Empty,
            vertexEntry: "vs_shared_page", fragmentEntry: variant == Entries.Length ? "fs_material_cached" : Entries[variant],
            targetFormat: compositor.RenderFormat, sampleCount: offscreen ? 1u : compositor.Options.PrimarySampleCount,
            pipelineLayout: _sharedReplayLayout, sourceAlphaMode: ProGPU.Backend.GpuTextureAlphaMode.Premultiplied);
        pipelines[variant] = (nint)pipeline; return pipeline;
    }

    private void DisposeSharedInstances()
    {
        if (_context is { IsDisposed: false } context)
        {
            if (_sharedStorageGroup != null) context.QueueBindGroupDisposal((nint)_sharedStorageGroup);
            if (_sharedStorageLayout != null) context.QueueBindGroupLayoutDisposal((nint)_sharedStorageLayout);
            if (_sharedReplayLayout != null) context.QueuePipelineLayoutDisposal((nint)_sharedReplayLayout);
        }
        _sourceStream?.Dispose(); _referenceStream?.Dispose(); _sourceStream = null; _referenceStream = null;
        _sourceInstances = null; _pageReferences = null; _pageSourceIndices = null; _sourceLookup = null; StaticSourceInstances = 0;
        _sharedStorageGroup = null; _sharedStorageLayout = null; _sharedReplayLayout = null;
        Array.Clear(_sharedOnscreen); Array.Clear(_sharedOffscreen); _sharedInstancesPrepared = false;
    }
}
