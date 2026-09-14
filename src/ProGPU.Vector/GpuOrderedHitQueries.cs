using System.Runtime.InteropServices;
using ProGPU.Backend;
using Silk.NET.WebGPU;

namespace ProGPU.Vector;

/// <summary>One retained index generation's staged query resources, on its original device.</summary>
internal unsafe sealed class GpuOrderedHitQueries : IDisposable
{
    private static readonly string Shader = ShaderResource.Load(typeof(GpuHitTestEngine), "GpuHitTesting.wgsl");
    private static readonly string[][] Entries = [
        ["cs_point_clip", "cs_point_bounds", "cs_point_rect_fill", "cs_point_rect_stroke", "cs_point_ellipse_fill", "cs_point_ellipse_stroke", "cs_point_line_stroke", "cs_point_path_fill", "cs_point_path_stroke", "cs_point_other"],
        ["cs_bounds_clip", "cs_bounds_bounds", "cs_bounds_rect_fill", "cs_bounds_rect_stroke", "cs_bounds_ellipse_fill", "cs_bounds_ellipse_stroke", "cs_bounds_line_stroke", "cs_bounds_path_fill", "cs_bounds_path_stroke", "cs_bounds_other"],
        ["cs_ellipse_clip", "cs_ellipse_bounds", "cs_ellipse_rect_fill", "cs_ellipse_rect_stroke", "cs_ellipse_ellipse_fill", "cs_ellipse_ellipse_stroke", "cs_ellipse_line_stroke", "cs_ellipse_path_fill", "cs_ellipse_path_stroke", "cs_ellipse_other"]
    ];
    private readonly WgpuContext _context;
    private readonly GpuHitTestDeviceIndex _index;
    private readonly RenderPipelineCache _cache;
    private readonly uint _families;
    private readonly nint[] _pipelines = new nint[32];
    private WgpuBindGroupLayoutLease? _layout;
    private WgpuPipelineLayoutLease? _pipelineLayout;
    private GpuBuffer? _candidates;
    private GpuBuffer? _arguments;
    private BindGroup* _pointGroup;
    private BindGroup* _listGroup;
    private bool _disposed;

    public GpuOrderedHitQueries(GpuHitTestDeviceIndex index)
    {
        _index = index;
        _context = index.Context;
        _cache = new RenderPipelineCache(_context);
        try
        {
            if (!_context.ComputeLimits.AdmitsOrderedHitQuery(index.PrimitiveIndexCount, _context.MaxBufferSize))
                throw new NotSupportedException("The device limits do not admit this ordered GPU hit-test index.");
            ulong maximum = _context.ComputeLimits.MaxStorageBufferBindingSize;
            if (index.NodeBuffer.Size > maximum || index.PrimitiveIndexBuffer.Size > maximum ||
                index.PrimitiveBuffer.Size > maximum || index.PathSegmentBuffer.Size > maximum ||
                index.ResultListBuffer.Size > maximum || index.QueryBuffer.Size > maximum)
                throw new NotSupportedException("An ordered GPU hit-test binding exceeds the device storage limit.");
            // Topology metadata only, once per immutable index; no CPU geometry.
            foreach (ref readonly var primitive in index.Index.PrimitiveSpan)
                _families |= 1u << (int)Math.Min((uint)primitive.Kind, 8u);
            _candidates = new GpuBuffer(_context, checked((uint)(32UL + 8UL * index.PrimitiveIndexCount)),
                BufferUsage.Storage | BufferUsage.CopySrc, "Ordered hit candidates");
            _arguments = new GpuBuffer(_context, 12, BufferUsage.Indirect | BufferUsage.CopyDst, "Ordered hit dispatch arguments");
            var entries = stackalloc BindGroupLayoutEntry[7];
            for (int i = 0; i < 7; i++) entries[i] = new() {
                Binding = i == 6 ? 7u : (uint)i, Visibility = ShaderStage.Compute,
                Buffer = new() { Type = i is 4 or 6 ? BufferBindingType.Storage : BufferBindingType.ReadOnlyStorage }
            };
            var descriptor = new BindGroupLayoutDescriptor { EntryCount = 7, Entries = entries };
            _layout = _context.AcquireSharedBindGroupLayout(new("ProGPU.Vector.GpuOrderedHitQueries", "Bindings"), &descriptor);
            var handle = _layout.Handle;
            var pipelineDescriptor = new PipelineLayoutDescriptor { BindGroupLayoutCount = 1, BindGroupLayouts = &handle };
            _pipelineLayout = _context.AcquireSharedPipelineLayout(new("ProGPU.Vector.GpuOrderedHitQueries", "Pipeline"), &pipelineDescriptor);
            _pointGroup = CreateGroup(index.ResultBuffer);
            _listGroup = CreateGroup(index.ResultListBuffer);
        }
        catch { Dispose(); throw; }
    }

    private BindGroup* CreateGroup(GpuBuffer result)
    {
        var entries = stackalloc BindGroupEntry[7];
        entries[0] = Binding(0, _index.QueryBuffer);
        entries[1] = Binding(1, _index.NodeBuffer);
        entries[2] = Binding(2, _index.PrimitiveIndexBuffer);
        entries[3] = Binding(3, _index.PrimitiveBuffer);
        entries[4] = Binding(4, result);
        entries[5] = Binding(5, _index.PathSegmentBuffer);
        entries[6] = Binding(7, _candidates!);
        var descriptor = new BindGroupDescriptor { Layout = _layout!.Handle, EntryCount = 7, Entries = entries };
        var group = _context.Api.DeviceCreateBindGroup(_context.Device, &descriptor);
        if (group == null) throw new InvalidOperationException("Could not create ordered query bindings.");
        return group;
    }

    private static BindGroupEntry Binding(uint binding, GpuBuffer buffer) =>
        new() { Binding = binding, Buffer = buffer.BufferPtr, Size = buffer.Size };

    public void Query(GpuHitTestQuery query, bool list, Span<byte> bytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int mode = (query.Flags & 0x80000000u) == 0 ? 0 : (query.Flags & 0x40000000u) == 0 ? 1 : 2;
        var shader = _cache.GetOrCreateShader("GpuHitTesting.Ordered", Shader);
        Prepare(0, "cs_collect");
        Prepare(1, "cs_merge");
        for (int family = 0; family < 10; family++)
            if (family == 0 || (_families & (1u << (family - 1))) != 0)
                Prepare(2 + mode * 10 + family, Entries[mode][family]);
        var results = MemoryMarshal.Cast<byte, GpuHitTestResult>(bytes);
        results.Fill(new() { Id = -1, PrimitiveIndex = uint.MaxValue, ZIndex = float.NegativeInfinity });
        var output = list ? _index.ResultListBuffer : _index.ResultBuffer;
        var group = list ? _listGroup : _pointGroup;
        _index.QueryBuffer.WriteSingle(query);
        output.Write<GpuHitTestResult>(results);
        var encoderDescriptor = new CommandEncoderDescriptor();
        var encoder = _context.Api.DeviceCreateCommandEncoder(_context.Device, &encoderDescriptor);
        if (encoder == null) throw new InvalidOperationException("Could not create ordered query encoder.");
        try
        {
            Dispatch(0, false);
            _context.Api.CommandEncoderCopyBufferToBuffer(encoder, _candidates!.BufferPtr, 16, _arguments!.BufferPtr, 0, 12);
            for (int family = 0; family < 10; family++)
                if (family == 0 || (_families & (1u << (family - 1))) != 0)
                    Dispatch(2 + mode * 10 + family, true);
            Dispatch(1, false);
            var descriptor = new CommandBufferDescriptor();
            var commands = _context.Api.CommandEncoderFinish(encoder, &descriptor);
            if (commands == null) throw new InvalidOperationException("Could not finish ordered query encoder.");
            try { _context.Submit(1, &commands); }
            finally { _context.Api.CommandBufferRelease(commands); }
        }
        finally { _context.Api.CommandEncoderRelease(encoder); }
        // Existing completion/readback semantics, with no intermediate CPU work.
        output.ReadBytes(bytes);
        if (results[0].Hit == uint.MaxValue)
            throw new InvalidOperationException("Ordered GPU hit-test candidate overflow; no result was published.");

        void Prepare(int slot, string entry)
        {
            if (_pipelines[slot] == 0)
                _pipelines[slot] = (nint)_cache.GetOrCreateComputePipeline(entry, shader, entry, _pipelineLayout!.Handle);
        }
        void Dispatch(int slot, bool indirect)
        {
            var descriptor = new ComputePassDescriptor();
            var pass = _context.Api.CommandEncoderBeginComputePass(encoder, &descriptor);
            if (pass == null) throw new InvalidOperationException("Could not begin ordered query pass.");
            try
            {
                _context.Api.ComputePassEncoderSetPipeline(pass, (ComputePipeline*)_pipelines[slot]);
                _context.Api.ComputePassEncoderSetBindGroup(pass, 0, group, 0, null);
                if (indirect) _context.Api.ComputePassEncoderDispatchWorkgroupsIndirect(pass, _arguments!.BufferPtr, 0);
                else _context.Api.ComputePassEncoderDispatchWorkgroups(pass, 1, 1, 1);
                _context.Api.ComputePassEncoderEnd(pass);
            }
            finally { _context.Api.ComputePassEncoderRelease(pass); }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_pointGroup != null) _context.QueueBindGroupDisposal((nint)_pointGroup);
        if (_listGroup != null) _context.QueueBindGroupDisposal((nint)_listGroup);
        _pointGroup = _listGroup = null;
        _cache.Dispose();
        _pipelineLayout?.Dispose();
        _layout?.Dispose();
        _arguments?.Dispose();
        _candidates?.Dispose();
    }
}
