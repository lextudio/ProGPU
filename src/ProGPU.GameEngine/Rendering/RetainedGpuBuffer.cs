using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProGPU.Backend;
using Silk.NET.WebGPU;

namespace ProGPU.GameEngine.Rendering;

/// <summary>
/// Fixed-capacity, device-owned stream with an owned CPU shadow. Update compares
/// exact bytes in O(N * sizeof(T)), then writes at most one enclosing changed range.
/// Unchanged replay performs no upload or allocation. Records must be four-byte
/// aligned; callers initialize all padding and consume only Count records. The
/// caller's span is borrowed synchronously and is never retained. Single-thread owned;
/// Buffer is exposed for binding/readback only, never for writes outside this owner.
/// </summary>
public sealed class RetainedGpuBuffer<T> : IDisposable where T : unmanaged
{
    private readonly T[] _shadow;
    private bool _disposed;
    private static readonly int Stride = Unsafe.SizeOf<T>();
    public GpuBuffer Buffer { get; }
    public int Capacity => _shadow.Length;
    public int Count { get; private set; }
    public uint Generation { get; private set; }
    public long UploadedBytes { get; private set; }
    public long WriteCount { get; private set; }

    public RetainedGpuBuffer(WgpuContext context, int capacity, BufferUsage usage, string label)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (Stride % 4 != 0) throw new ArgumentException("GPU stream records must have a four-byte-aligned size.");
        uint bytes = checked((uint)capacity * (uint)Stride);
        _shadow = new T[capacity];
        Buffer = new(context, bytes, usage | BufferUsage.CopyDst, label);
    }

    public bool Update(ReadOnlySpan<T> records)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (records.Length > Capacity) throw new ArgumentOutOfRangeException(nameof(records));
        var next = MemoryMarshal.AsBytes(records);
        var previous = MemoryMarshal.AsBytes(_shadow.AsSpan());
        int common = Math.Min(records.Length, Count), first = 0;
        while (first < common && next.Slice(first * Stride, Stride).SequenceEqual(previous.Slice(first * Stride, Stride))) first++;
        if (first == records.Length && records.Length == Count) return false;
        int end = records.Length;
        // Never skip the newly exposed tail after growth, even if old shadow bytes
        // happen to match. Shrink changes draw count without clearing GPU memory.
        if (records.Length <= Count)
            while (end > first && next.Slice((end - 1) * Stride, Stride).SequenceEqual(previous.Slice((end - 1) * Stride, Stride))) end--;
        if (end > first)
        {
            var changed = records.Slice(first, end - first);
            Buffer.Write(changed, checked((uint)(first * Stride)));
            changed.CopyTo(_shadow.AsSpan(first));
            UploadedBytes += (long)changed.Length * Stride; WriteCount++;
        }
        Count = records.Length; Generation++;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Buffer.Dispose(); _disposed = true; Count = 0;
    }
}
