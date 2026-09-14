using ProGPU.GameEngine.Simulation;

namespace ProGPU.GameEngine.Rendering;

public readonly record struct SpatialItem2D(int Id, Bounds2D Bounds);

/// <summary>
/// Immutable CPU bounding-volume hierarchy for one scene generation. Original
/// implementation: quantize centroids on a 16-bit lattice per axis, interleave bits
/// for spatial ordering, then build a balanced median tree over that order. Bounds
/// retain their original double precision; quantization affects locality only.
/// Build costs O(N log N) time/O(N) storage. Query costs O(Q + V log V), with Q
/// visited nodes and V results, worst O(N + V log V). Sorting result IDs preserves
/// caller-authored painter order. Querying uses a bounded 64-entry stack and caller
/// result storage, with no allocation, retained pointers, GPU calls or callbacks.
/// Callers include animation/art extents and replace the index when they change.
/// </summary>
public sealed class StaticSpatialIndex2D
{
    public const int MaximumItems = 131_072;
    private readonly record struct OrderedItem(SpatialItem2D Item, uint Key);
    // Negative First encodes the complemented leaf ID; otherwise both are children.
    private readonly record struct Node(Bounds2D Bounds, int First, int Second);
    private readonly Node[] _nodes;
    public int Count { get; }

    public StaticSpatialIndex2D(ReadOnlySpan<SpatialItem2D> items)
    {
        if (items.Length > MaximumItems) throw new ArgumentOutOfRangeException(nameof(items));
        Count = items.Length;
        if (items.IsEmpty) { _nodes = []; return; }
        var identities = new HashSet<int>(items.Length);
        Bounds2D scene = default;
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (item.Id < 0 || !item.Bounds.IsValid || !identities.Add(item.Id))
                throw new ArgumentException("Spatial records require distinct nonnegative IDs and finite positive rectangles.", nameof(items));
            scene = i == 0 ? item.Bounds : Union(scene, item.Bounds);
        }
        if (!scene.IsValid) throw new ArgumentException("The combined spatial extent must remain finite.", nameof(items));
        var order = new OrderedItem[Count];
        for (int i = 0; i < Count; i++)
        {
            var b = items[i].Bounds;
            uint x = (uint)Math.Clamp((b.X + b.Width / 2 - scene.X) / scene.Width * 65535, 0, 65535);
            uint y = (uint)Math.Clamp((b.Y + b.Height / 2 - scene.Y) / scene.Height * 65535, 0, 65535);
            uint key = 0;
            // Fixed 16 iterations, no external lookup table or encoded implementation.
            for (int bit = 0; bit < 16; bit++)
            { key |= ((x >> bit) & 1) << (bit * 2); key |= ((y >> bit) & 1) << (bit * 2 + 1); }
            order[i] = new(items[i], key);
        }
        Array.Sort(order, static (a, b) => { int c = a.Key.CompareTo(b.Key); return c != 0 ? c : a.Item.Id.CompareTo(b.Item.Id); });
        _nodes = new Node[Count * 2 - 1]; int next = 0;
        Build(order, 0, Count, ref next);
    }
    private int Build(OrderedItem[] order, int start, int count, ref int next)
    {
        int index = next++;
        if (count == 1) { var item = order[start].Item; _nodes[index] = new(item.Bounds, ~item.Id, 0); return index; }
        int leftCount = count / 2;
        int left = Build(order, start, leftCount, ref next);
        int right = Build(order, start + leftCount, count - leftCount, ref next);
        _nodes[index] = new(Union(_nodes[left].Bounds, _nodes[right].Bounds), left, right); return index;
    }
    private static Bounds2D Union(Bounds2D a, Bounds2D b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        double right = Math.Max(a.Right, b.Right), bottom = Math.Max(a.Bottom, b.Bottom);
        double width = right - x, height = bottom - y;
        // Reconstructing a large extent must never round a parent inside a child.
        if (x + width < right) width = Math.BitIncrement(width);
        if (y + height < bottom) height = Math.BitIncrement(height);
        return new(x, y, width, height);
    }

    /// <summary>
    /// Write touching/intersecting IDs in ascending authored order. The reusable
    /// destination must accommodate Count entries; results are never truncated.
    /// Only the returned prefix is valid. At the maximum count the balanced tree
    /// has 18 levels, within the fixed 64-entry depth-first traversal stack.
    /// </summary>
    public int QueryOrdered(Bounds2D viewport, Span<int> destination)
    {
        if (!viewport.IsValid) throw new ArgumentException("A viewport must be a finite positive rectangle.", nameof(viewport));
        if (destination.Length < Count) throw new ArgumentException("Spatial query storage must accommodate the full index count.", nameof(destination));
        if (Count == 0) return 0;
        Span<int> stack = stackalloc int[64]; int pending = 1, found = 0; stack[0] = 0;
        while (pending > 0)
        {
            var node = _nodes[stack[--pending]]; var b = node.Bounds;
            if (b.Right < viewport.X || b.X > viewport.Right || b.Bottom < viewport.Y || b.Y > viewport.Bottom) continue;
            if (node.First < 0) { destination[found++] = ~node.First; continue; }
            stack[pending++] = node.Second; stack[pending++] = node.First;
        }
        destination[..found].Sort(); return found;
    }
}
