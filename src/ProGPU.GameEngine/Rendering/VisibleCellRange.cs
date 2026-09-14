namespace ProGPU.GameEngine.Rendering;

/// <summary>
/// A half-open range of repeated-cell indices whose conservative artwork footprint
/// may intersect a viewport interval. Selection is O(1), iteration is O(V) visible
/// cells plus at most two guard cells. No scan proportional to the whole world.
/// The caller's footprint includes shape jitter and placement error beyond one cell;
/// rebase coordinates when float32 precision approaches the cell spacing.
/// </summary>
public readonly record struct VisibleCellRange(int Start, int End)
{
    public int Count => End - Start;

    /// <param name="origin">Anchor of cell zero in world units.</param>
    /// <param name="step">Positive world-unit distance between anchors.</param>
    /// <param name="count">Total number of cells in the source segment.</param>
    /// <param name="minimum">Viewport lower coordinate, inclusive.</param>
    /// <param name="maximum">Viewport upper coordinate, inclusive.</param>
    /// <param name="before">Maximum artwork extension before its cell anchor.</param>
    /// <param name="after">Maximum artwork extension after its cell anchor.</param>
    public static VisibleCellRange Intersect(float origin, float step, int count,
        float minimum, float maximum, float before, float after)
    {
        if (!float.IsFinite(origin)) throw new ArgumentOutOfRangeException(nameof(origin));
        if (!float.IsFinite(step) || step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (!float.IsFinite(minimum)) throw new ArgumentOutOfRangeException(nameof(minimum));
        if (!float.IsFinite(maximum) || maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
        if (!float.IsFinite(before) || before < 0) throw new ArgumentOutOfRangeException(nameof(before));
        if (!float.IsFinite(after) || after < 0) throw new ArgumentOutOfRangeException(nameof(after));
        // Double arithmetic avoids overflow in coordinate subtraction. One extra
        // cell at each side keeps float32 placement rounding conservative at edges.
        // Clamp in double before casting, including completely offscreen segments.
        double first = Math.Ceiling(((double)minimum - origin - after) / step) - 1;
        double end = Math.Floor(((double)maximum - origin + before) / step) + 2;
        int startIndex = (int)Math.Clamp(first, 0, count);
        int endIndex = (int)Math.Clamp(end, 0, count);
        return new(startIndex, Math.Max(startIndex, endIndex));
    }
}
