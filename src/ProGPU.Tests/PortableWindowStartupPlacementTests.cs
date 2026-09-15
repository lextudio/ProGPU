using ProGPU.Wpf.Interop;
using Xunit;

namespace ProGPU.Tests;

public class PortableWindowStartupPlacementTests
{
    [Fact]
    public void CenterScreenUsesWorkAreaAndPreservesNegativeDesktopOrigin()
    {
        Assert.True(PortableWindowStartupPlacement.TryCenterScreen(
            new(-1920, 24, 1920, 1056), 800, 600, out double left, out double top));
        Assert.Equal(-1360, left);
        Assert.Equal(252, top);
    }

    [Fact]
    public void CenterOwnerClampsToSelectedMonitorWorkArea()
    {
        Assert.True(PortableWindowStartupPlacement.TryCenterOwner(
            new(1800, 800, 200, 200), new(0, 0, 1920, 1040), 600, 400,
            out double left, out double top));
        Assert.Equal(1320, left);
        Assert.Equal(640, top);
    }

    [Fact]
    public void OversizedOwnerWindowStartsAtWorkAreaOrigin()
    {
        Assert.True(PortableWindowStartupPlacement.TryCenterOwner(
            new(20, 20, 100, 100), new(0, 0, 300, 200), 400, 300,
            out double left, out double top));
        Assert.Equal(0, left);
        Assert.Equal(0, top);
    }

    [Fact]
    public void InvalidGeometryDoesNotPublishCoordinates()
    {
        Assert.False(PortableWindowStartupPlacement.TryCenterScreen(
            new(0, 0, 0, 100), 50, 50, out _, out _));
        Assert.False(PortableWindowStartupPlacement.TryCenterOwner(
            PortableRect.Empty, new(0, 0, 100, 100), 50, 50, out _, out _));
        Assert.False(PortableWindowStartupPlacement.TryCenterScreen(
            new(0, 0, 100, 100), double.NaN, 50, out _, out _));
    }
}
