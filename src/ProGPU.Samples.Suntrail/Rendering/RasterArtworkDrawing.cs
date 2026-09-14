using System.Numerics;
using System.Runtime.InteropServices;
using ProGPU.Backend;
using ProGPU.GameEngine.Assets;
using ProGPU.GameEngine.Rendering;
using ProGPU.Scene;
using ProGPU.Vector;
using Silk.NET.WebGPU;

namespace ProGPU.Samples.Suntrail.Rendering;

/// <summary>Immutable retained image-view command. Owner identifies one UI view, never a GPU device.</summary>
public sealed record RasterArtworkDraw(object Owner, RasterImage Image, Rect Destination, Rect Source);

public readonly record struct RasterArtworkSprite(RasterImage Image, Rect Destination, Rect Source)
{
    /// <summary>Zero for an ordinary sprite; otherwise source border X/Y and logical pixels per source pixel X/Y for tiled nine-slice sizing.</summary>
    public Vector4 NineSlice { get; init; }
}

/// <summary>Owned immutable painter-ordered snapshot. Construct after content changes, not on stable replay.</summary>
public sealed class RasterArtworkBatch
{
    public const int MaximumSprites = 65_536;
    private readonly RasterArtworkSprite[] _sprites;
    private RasterArtworkPipeline.Geometry? _geometry;
    private RasterArtworkPipeline.Compiled? _compiled;
    public object Owner { get; }
    public Rect Bounds { get; }
    public ReadOnlySpan<RasterArtworkSprite> Sprites => _sprites;
    public RasterArtworkBatch(object owner, ReadOnlySpan<RasterArtworkSprite> sprites)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (sprites.Length > MaximumSprites) throw new ArgumentOutOfRangeException(nameof(sprites));
        Owner = owner;
        float left = float.PositiveInfinity, top = float.PositiveInfinity, right = float.NegativeInfinity, bottom = float.NegativeInfinity;
        foreach (var sprite in sprites)
        {
            var s = sprite.Source; var d = sprite.Destination;
            if (sprite.Image is null || !Finite(s) || !Finite(d) || s.X < 0 || s.Y < 0 ||
                s.Width < 1 || s.Height < 1 || s.X + s.Width > sprite.Image.Width || s.Y + s.Height > sprite.Image.Height || d.Width <= 0 || d.Height <= 0 ||
                s.X != MathF.Truncate(s.X) || s.Y != MathF.Truncate(s.Y) || s.Width != MathF.Truncate(s.Width) || s.Height != MathF.Truncate(s.Height))
                throw new ArgumentException("Artwork needs finite destination geometry and a whole-pixel source rectangle inside its image.", nameof(sprites));
            var n = sprite.NineSlice;
            if (n != Vector4.Zero && (!float.IsFinite(n.X) || !float.IsFinite(n.Y) || !float.IsFinite(n.Z) || !float.IsFinite(n.W) ||
                n.X < 1 || n.Y < 1 || n.X != MathF.Truncate(n.X) || n.Y != MathF.Truncate(n.Y) ||
                2 * n.X >= s.Width || 2 * n.Y >= s.Height || n.Z <= 0 || n.W <= 0 ||
                d.Width < 2 * n.X * n.Z || d.Height < 2 * n.Y * n.W || d.Width / n.Z > 65_536 || d.Height / n.W > 65_536))
                throw new ArgumentException("Nine-slice sprites require integral borders, a nonempty center, positive pixel scale and destination extents between two borders and 65,536 source pixels.", nameof(sprites));
            left = MathF.Min(left, d.X); top = MathF.Min(top, d.Y);
            right = MathF.Max(right, d.X + d.Width); bottom = MathF.Max(bottom, d.Y + d.Height);
        }
        Bounds = sprites.IsEmpty ? default : new(left, top, right - left, bottom - top);
        if (!sprites.IsEmpty && !Finite(Bounds)) throw new ArgumentException("Combined artwork bounds must be finite.", nameof(sprites));
        _sprites = sprites.ToArray();
    }
    // CPU-only immutable packing is shared across compositor devices. Races may
    // prepare independent candidates at first use; only one snapshot is retained.
    // Opacity changes reuse geometry, while unchanged compilation reuses its payload.
    internal RasterArtworkPipeline.Compiled Compile(float opacity)
    {
        var existing = Volatile.Read(ref _compiled);
        if (existing is not null && existing.Opacity == opacity) return existing;
        var geometry = Volatile.Read(ref _geometry);
        if (geometry is null)
        {
            var candidate = RasterArtworkPipeline.Pack(this);
            geometry = Interlocked.CompareExchange(ref _geometry, candidate, null) ?? candidate;
        }
        var next = new RasterArtworkPipeline.Compiled(this, opacity, geometry);
        Volatile.Write(ref _compiled, next); return next;
    }
    private static bool Finite(Rect r) => float.IsFinite(r.X) && float.IsFinite(r.Y) && float.IsFinite(r.Width) && float.IsFinite(r.Height) &&
        float.IsFinite(r.X + r.Width) && float.IsFinite(r.Y + r.Height);
}

public static class RasterArtworkDrawing
{
    public static readonly DrawingExtension<RasterArtworkBatch> Definition = new("Suntrail prepared artwork", static () => new RasterArtworkPipeline());
    public static void DrawArtwork(this DrawingContext context, RasterArtworkDraw draw) =>
        context.DrawArtworkBatch(new(draw.Owner, [new(draw.Image, draw.Destination, draw.Source)]));
    public static void DrawArtworkBatch(this DrawingContext context, RasterArtworkBatch batch, Matrix4x4 transform = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (!batch.Sprites.IsEmpty) context.DrawExtension(Definition, batch.Bounds, batch, transform);
    }
}

/// <summary>
/// Bounded painter-ordered artwork: eight retained batch owners, up to 65,536 sprites
/// per batch, 128 images and 32 MiB texture residency shared with single-image views.
/// Compile packs O(N) instances and contiguous image runs without sorting. Preparation
/// uploads new images once and changed instance/uniform ranges only. Stable replay
/// creates no resources or arrays and issues one instanced draw per contiguous run.
/// </summary>
public sealed unsafe class RasterArtworkPipeline : ICompositorExtension, IDisposable
{
    internal readonly record struct ImageRun(RasterImage Image, uint First, uint Count);
    internal sealed record Geometry(Instance[] Instances, ImageRun[] Runs, RasterImage[] Images);
    internal sealed record Compiled(RasterArtworkBatch Batch, float Opacity, Geometry Data)
    {
        public Instance[] Instances => Data.Instances;
        public ImageRun[] Runs => Data.Runs;
        public RasterImage[] Images => Data.Images;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Instance { public Vector4 Destination, Source, NineSlice; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Uniforms { public Matrix4x4 Matrix; public Vector4 Opacity; }
    private sealed class View : IDisposable
    {
        public readonly RetainedGpuBuffer<Uniforms> Uniforms;
        public readonly RetainedGpuBuffer<Instance> Instances;
        public Compiled? Prepared;
        public nint Pipeline;
        public nint Group;
        public bool Used;
        private readonly WgpuContext _context;
        public View(WgpuContext context, int capacity)
        {
            _context = context; Uniforms = new(context, 1, BufferUsage.Uniform, "Artwork frame");
            try { Instances = new(context, capacity, BufferUsage.Storage, "Artwork sprites"); }
            catch { Uniforms.Dispose(); throw; }
        }
        public void Dispose() { if (Group != 0 && !_context.IsDisposed) _context.QueueBindGroupDisposal(Group); Group = 0; Uniforms.Dispose(); Instances.Dispose(); Prepared = null; }
    }
    private sealed class ImageResource : IDisposable
    {
        public readonly GpuTexture Texture;
        public nint Group;
        public bool Used;
        public ImageResource(GpuTexture texture) => Texture = texture;
        public void Dispose()
        { if (Group != 0 && !Texture.Context.IsDisposed) Texture.Context.QueueBindGroupDisposal(Group); Group = 0; Texture.Dispose(); }
    }
    private readonly Dictionary<object, View> _views = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<RasterImage, ImageResource> _images = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _oldViews = new(8);
    private readonly List<RasterImage> _oldImages = new(128);
    private WgpuContext? _context;
    private RenderPipelineCache? _cache;
    private BindGroupLayout* _uniformLayout, _imageLayout;
    private PipelineLayout* _layout;
    private nint _pipeline;
    private TextureFormat _format;
    private uint _samples;
    private static readonly string Shader = ShaderResource.Load(typeof(RasterArtworkPipeline), "RasterArtwork.wgsl");
    public long UploadedPixelBytes { get; private set; }
    public long UploadedUniformBytes { get; private set; }
    public int ResidentPixelBytes { get; private set; }
    public long Draws { get; private set; }
    public long UploadedInstanceBytes { get; private set; }

    public void Compile(Compositor compositor, IRenderDataProvider? provider, Matrix4x4 transform, ref RenderCommand cmd)
    {
        if (cmd.DataParam is not RasterArtworkBatch batch) throw new ArgumentException("Expected prepared artwork batch.");
        cmd.DataParam = batch.Compile(compositor.ActiveOpacity);
    }
    internal static Geometry Pack(RasterArtworkBatch batch)
    {
        var sprites = batch.Sprites; var instances = new Instance[sprites.Length];
        var runs = new List<ImageRun>();
        var images = new HashSet<RasterImage>(ReferenceEqualityComparer.Instance); long bytes = 0;
        for (int i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i]; var d = sprite.Destination; var source = sprite.Source;
            if (images.Add(sprite.Image)) bytes += sprite.Image.ByteLength;
            if (images.Count > 128 || bytes > 32 * 1024 * 1024)
                throw new InvalidOperationException("An artwork batch exceeds its 32 MiB / 128 image residency budget.");
            instances[i] = new() { Destination = new(d.X, d.Y, d.Width, d.Height), Source = new(source.X, source.Y, source.Width, source.Height), NineSlice = sprite.NineSlice };
            if (runs.Count > 0 && ReferenceEquals(runs[^1].Image, sprite.Image))
                runs[^1] = runs[^1] with { Count = runs[^1].Count + 1 };
            else runs.Add(new(sprite.Image, (uint)i, 1));
        }
        return new(instances, runs.ToArray(), images.ToArray());
    }
    public void BeginFrame(Compositor compositor)
    { foreach (var view in _views.Values) view.Used = false; foreach (var image in _images.Values) image.Used = false; }

    public bool TryPrepareDrawCall(Compositor compositor, bool isOffscreen, in Compositor.CompositorDrawCall drawCall, out Compositor.CompositorDrawCall preparedDrawCall)
    {
        preparedDrawCall = drawCall;
        if (drawCall.DataParam is not Compiled compiled) return false;
        if (drawCall.MaskTexture is not null || drawCall.MaskBindGroupOverride != 0 || drawCall.BlendMode != GpuBlendMode.SrcOver)
            throw new NotSupportedException("Artwork batches currently support rectangular clipping and source-over composition.");
        Ensure(compositor, isOffscreen);
        var batch = compiled.Batch;
        if (_views.TryGetValue(batch.Owner, out var view) && view.Used)
            throw new InvalidOperationException("An artwork batch owner was recorded more than once in the frame.");
        if (view is not null && view.Instances.Capacity < compiled.Instances.Length)
        { view.Dispose(); _views.Remove(batch.Owner); view = null; }
        if (view is null)
        {
            if (_views.Count == 8) RemoveUnusedViews();
            if (_views.Count == 8) throw new InvalidOperationException("At most eight artwork batch owners may render together.");
            int capacity = 1; while (capacity < compiled.Instances.Length) capacity *= 2;
            view = new View(_context!, capacity);
            try
            {
                var entries = stackalloc BindGroupEntry[2];
                entries[0] = new() { Binding = 0, Buffer = view.Uniforms.Buffer.BufferPtr, Size = 80 };
                entries[1] = new() { Binding = 1, Buffer = view.Instances.Buffer.BufferPtr, Size = (ulong)capacity * 48 };
                var descriptor = new BindGroupDescriptor { Layout = _uniformLayout, EntryCount = 2, Entries = entries };
                view.Group = (nint)_context!.Api.DeviceCreateBindGroup(_context.Device, &descriptor); _views.Add(batch.Owner, view);
            }
            catch { view.Dispose(); throw; }
        }
        view.Used = true; view.Pipeline = _pipeline;
        // Pin every already-resident image used by this batch before making space.
        // Otherwise an early new image could evict a later run's retained image.
        foreach (var source in compiled.Images)
            if (_images.TryGetValue(source, out var resident)) resident.Used = true;
        foreach (var source in compiled.Images) PrepareImage(source);
        if (!ReferenceEquals(view.Prepared?.Batch, compiled.Batch))
        {
            long instanceBefore = view.Instances.UploadedBytes; view.Instances.Update(compiled.Instances);
            UploadedInstanceBytes += view.Instances.UploadedBytes - instanceBefore; view.Prepared = compiled;
        }
        Span<Uniforms> data = stackalloc Uniforms[1];
        data[0] = new() { Matrix = drawCall.Transform * compositor.CurrentProjection, Opacity = new(compiled.Opacity, 0, 0, 0) };
        long before = view.Uniforms.UploadedBytes; view.Uniforms.Update(data); UploadedUniformBytes += view.Uniforms.UploadedBytes - before;
        return false;
    }

    private void PrepareImage(RasterImage source)
    {
        if (!_images.TryGetValue(source, out var image))
        {
            if (ResidentPixelBytes + source.ByteLength > 32 * 1024 * 1024 || _images.Count == 128) RemoveUnusedImages();
            if (ResidentPixelBytes + source.ByteLength > 32 * 1024 * 1024 || _images.Count == 128)
                throw new InvalidOperationException("Artwork views exceed their 32 MiB / 128 image residency budget.");
            image = new(new GpuTexture(_context!, (uint)source.Width, (uint)source.Height, TextureFormat.Rgba8Unorm,
                TextureUsage.TextureBinding | TextureUsage.CopyDst, "Prepared source artwork"));
            try
            {
                image.Texture.WritePixels<byte>(source.Pixels);
                var entry = new BindGroupEntry { Binding = 0, TextureView = image.Texture.ViewPtr };
                var descriptor = new BindGroupDescriptor { Layout = _imageLayout, EntryCount = 1, Entries = &entry };
                image.Group = (nint)_context!.Api.DeviceCreateBindGroup(_context.Device, &descriptor); _images.Add(source, image);
                ResidentPixelBytes += source.ByteLength; UploadedPixelBytes += source.ByteLength;
            }
            catch { image.Dispose(); throw; }
        }
        image.Used = true;
    }

    public void Render(Compositor compositor, void* encoder, bool isOffscreen, in Compositor.CompositorDrawCall dc)
    {
        if (dc.DataParam is not Compiled compiled) return;
        var view = _views[compiled.Batch.Owner];
        var api = compositor.Context.Api; var pass = (RenderPassEncoder*)encoder;
        api.RenderPassEncoderSetPipeline(pass, (RenderPipeline*)view.Pipeline);
        api.RenderPassEncoderSetBindGroup(pass, 0, (BindGroup*)view.Group, 0, null);
        foreach (var run in compiled.Runs)
        {
            api.RenderPassEncoderSetBindGroup(pass, 1, (BindGroup*)_images[run.Image].Group, 0, null);
            api.RenderPassEncoderDraw(pass, 6, run.Count, 0, run.First); Draws++;
        }
    }

    private void Ensure(Compositor compositor, bool offscreen)
    {
        if (_context is not null && !ReferenceEquals(_context, compositor.Context)) throw new InvalidOperationException("Artwork resources belong to one compositor device.");
        _context ??= compositor.Context;
        var api = _context.Api;
        if (_cache is null)
        {
            _cache = new(_context);
            var uniforms = stackalloc BindGroupLayoutEntry[2];
            uniforms[0] = new() { Binding = 0, Visibility = ShaderStage.Vertex | ShaderStage.Fragment,
                Buffer = new() { Type = BufferBindingType.Uniform, MinBindingSize = 80 } };
            uniforms[1] = new() { Binding = 1, Visibility = ShaderStage.Vertex,
                Buffer = new() { Type = BufferBindingType.ReadOnlyStorage, MinBindingSize = 48 } };
            var descriptor = new BindGroupLayoutDescriptor { EntryCount = 2, Entries = uniforms };
            _uniformLayout = api.DeviceCreateBindGroupLayout(_context.Device, &descriptor);
            var image = new BindGroupLayoutEntry { Binding = 0, Visibility = ShaderStage.Fragment,
                Texture = new() { SampleType = TextureSampleType.Float, ViewDimension = TextureViewDimension.Dimension2D } };
            descriptor.EntryCount = 1; descriptor.Entries = &image; _imageLayout = api.DeviceCreateBindGroupLayout(_context.Device, &descriptor);
            var layouts = stackalloc BindGroupLayout*[2]; layouts[0] = _uniformLayout; layouts[1] = _imageLayout;
            var pipeline = new PipelineLayoutDescriptor { BindGroupLayoutCount = 2, BindGroupLayouts = layouts };
            _layout = api.DeviceCreatePipelineLayout(_context.Device, &pipeline);
        }
        uint samples = offscreen ? 1u : compositor.Options.PrimarySampleCount;
        if (_pipeline != 0 && _format == compositor.RenderFormat && _samples == samples) return;
        _format = compositor.RenderFormat; _samples = samples;
        var module = _cache.GetOrCreateShader("Artwork sprites", Shader, "Prepared image sampling");
        _pipeline = (nint)_cache.GetOrCreateRenderPipeline($"Artwork.{_format}.{samples}", module, ReadOnlySpan<VertexBufferLayout>.Empty,
            targetFormat: _format, sampleCount: samples, pipelineLayout: _layout, sourceAlphaMode: GpuTextureAlphaMode.Premultiplied);
    }

    public void EndFrame(Compositor compositor) { RemoveUnusedViews(); RemoveUnusedImages(); }
    private void RemoveUnusedViews()
    {
        _oldViews.Clear(); foreach (var pair in _views) if (!pair.Value.Used) _oldViews.Add(pair.Key);
        foreach (var key in _oldViews) { _views[key].Dispose(); _views.Remove(key); } _oldViews.Clear();
    }
    private void RemoveUnusedImages()
    {
        _oldImages.Clear(); foreach (var pair in _images) if (!pair.Value.Used) _oldImages.Add(pair.Key);
        foreach (var key in _oldImages) { _images[key].Dispose(); _images.Remove(key); ResidentPixelBytes -= key.ByteLength; } _oldImages.Clear();
    }
    public void Dispose()
    {
        foreach (var view in _views.Values) view.Dispose(); _views.Clear();
        foreach (var image in _images.Values) image.Dispose(); _images.Clear(); ResidentPixelBytes = 0;
        if (_context is { IsDisposed: false } context)
        {
            if (_layout != null) context.QueuePipelineLayoutDisposal((nint)_layout);
            if (_uniformLayout != null) context.QueueBindGroupLayoutDisposal((nint)_uniformLayout);
            if (_imageLayout != null) context.QueueBindGroupLayoutDisposal((nint)_imageLayout);
        }
        _cache?.Dispose(); _cache = null; _layout = null; _uniformLayout = _imageLayout = null; _pipeline = 0; _context = null;
    }
}
