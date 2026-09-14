using System.Collections.Immutable;
using ProGPU.GameEngine.Rendering;

namespace ProGPU.Samples.Suntrail.Game;

public sealed partial class Level
{
    internal StaticSpatialIndex2D PlatformVisibility { get; private set; } = null!;
    public uint GeometryGeneration { get; private set; }
    private float _replacementWidth;

    private static StaticSpatialIndex2D BuildPlatformVisibility(ImmutableArray<Platform> platforms)
    {
        var items = new SpatialItem2D[platforms.Length];
        for (int i = 0; i < items.Length; i++)
        {
            var p = platforms[i]; var b = p.Bounds;
            // Conveyor travel is surface speed, not spatial displacement. Other
            // platform trajectories are bounded by their authored sine amplitudes.
            double dx = p.Kind == PlatformKind.Conveyor ? 0 : Math.Abs(p.Travel);
            double dy = p.Kind == PlatformKind.Conveyor ? 0 : Math.Abs(p.VerticalTravel);
            // Conservative original art footprint: crowns extend 295 units up,
            // foliage and edge noise extend sideways; crumble shake is <=1.5 units.
            items[i] = new(i, new(b.X - dx - 256, b.Y - dy - 350,
                b.Width + dx * 2 + 512, b.Height + dy * 2 + 450));
        }
        return new(items);
    }

    /// <summary>
    /// Replace a non-pipe platform between simulation frames. Publishes an immutable
    /// platform array and spatial generation together, resets that support's timer,
    /// and invalidates paused rendering through GeometryGeneration. Pipe graph
    /// geometry is edited through LevelDocument instead. O(N log N) preparation;
    /// this is an authoring/update seam, never a per-frame movement API.
    /// </summary>
    public void ReplacePlatform(int index, Platform platform)
    {
        if ((uint)index >= Platforms.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (Platforms[index].Kind == PlatformKind.Pipe || platform.Kind == PlatformKind.Pipe)
            throw new ArgumentException("Edit connected pipe geometry through a new level document.", nameof(platform));
        var b = platform.Bounds;
        if (!Enum.IsDefined(platform.Kind) || !float.IsFinite(b.X) || !float.IsFinite(b.Y) ||
            !float.IsFinite(b.Width) || !float.IsFinite(b.Height) || !float.IsFinite(platform.Travel) ||
            !float.IsFinite(platform.VerticalTravel) || !float.IsFinite(platform.Phase) ||
            b.X < 0 || b.Right > 32_000 || b.Y < 0 || b.Y > 950 || b.Width <= 0 || b.Height <= 0 || b.Bottom > 1550 ||
            Math.Abs(platform.Travel) > 500 || Math.Abs(platform.VerticalTravel) > 300 || Math.Abs(platform.Phase) > 100 ||
            (platform.Kind == PlatformKind.Conveyor && platform.VerticalTravel != 0))
            throw new ArgumentException("Replacement platform geometry or motion exceeds the supported level bounds.", nameof(platform));
        if (Platforms[index] == platform) return;
        var next = Platforms.SetItem(index, platform); var visibility = BuildPlatformVisibility(next);
        float width = 0;
        foreach (var item in next) width = Math.Max(width, item.Bounds.Right + (item.Kind == PlatformKind.Conveyor ? 0 : Math.Abs(item.Travel)) + 100);
        Platforms = next; PlatformVisibility = visibility; _replacementWidth = width;
        _supportStates[index] = default; GeometryGeneration++;
    }
}
