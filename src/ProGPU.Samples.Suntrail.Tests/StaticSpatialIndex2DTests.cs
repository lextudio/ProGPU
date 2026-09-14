using ProGPU.GameEngine.Rendering;
using ProGPU.GameEngine.Simulation;
using ProGPU.Samples.Suntrail.Game;
using Xunit;

namespace ProGPU.Samples.Suntrail.Tests;

public sealed class StaticSpatialIndex2DTests
{
    [Fact]
    public void RandomViewportsMatchClosedBoundsBruteForceInPainterOrder()
    {
        var random = new Random(0x51A7);
        var items = new SpatialItem2D[513];
        for (int i = 0; i < items.Length; i++)
            items[i] = new(i * 2, new(random.Next(-4_000, 4_000) + random.NextDouble(),
                random.Next(-3_000, 3_000) + random.NextDouble(), random.Next(1, 800), random.Next(1, 600)));
        var index = new StaticSpatialIndex2D(items);
        var destination = new int[items.Length];
        for (int query = 0; query < 200; query++)
        {
            var view = new Bounds2D(random.Next(-5_000, 5_000), random.Next(-4_000, 4_000),
                random.Next(1, 1_500), random.Next(1, 1_200));
            int found = index.QueryOrdered(view, destination);
            int[] expected = items.Where(item => Intersects(item.Bounds, view)).Select(item => item.Id).ToArray();
            Assert.Equal(expected, destination.AsSpan(0, found).ToArray());
        }

        var touching = new StaticSpatialIndex2D([new(5, new(0, 0, 10, 10))]);
        Assert.Equal(1, touching.QueryOrdered(new(10, 10, 1, 1), destination));
        Assert.Equal(5, destination[0]);
        Assert.Equal(0, touching.QueryOrdered(new(10.001, 10, 1, 1), destination));
    }

    [Fact]
    public void ReplacementPublishesAVisibleGenerationAndRejectsPipeEdits()
    {
        var level = new Level(0);
        int platform = -1;
        for (int i = 0; i < level.Platforms.Length; i++)
            if (level.Platforms[i].Kind == PlatformKind.Ground) { platform = i; break; }
        Assert.True(platform >= 0);
        var previous = level.Platforms[platform];
        uint generation = level.GeometryGeneration;
        level.ReplacePlatform(platform, previous);
        Assert.Equal(generation, level.GeometryGeneration);
        var moved = previous with { Bounds = previous.Bounds with { X = previous.Bounds.X + 100 } };
        level.ReplacePlatform(platform, moved);
        Assert.Equal(generation + 1, level.GeometryGeneration);
        Assert.Equal(moved, level.Platforms[platform]);
        int pipe = -1;
        for (int i = 0; i < level.Platforms.Length; i++)
            if (level.Platforms[i].Kind == PlatformKind.Pipe) { pipe = i; break; }
        Assert.True(pipe >= 0);
        Assert.Throws<ArgumentException>(() => level.ReplacePlatform(pipe, level.Platforms[pipe]));
    }

    private static bool Intersects(Bounds2D a, Bounds2D b) =>
        a.Right >= b.X && a.X <= b.Right && a.Bottom >= b.Y && a.Y <= b.Bottom;
}
