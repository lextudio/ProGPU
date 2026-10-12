using ProGPU.Backend;
using ProGPU.Backend.Dawn;
using SW = Silk.NET.WebGPU;
using W = WebGpuSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class DawnSurfacePresentModeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryAdvertisedSubsetMatchesTheExistingNativeProviderPolicy(bool vsync)
    {
        W.PresentMode[] dawn =
            [W.PresentMode.Mailbox, W.PresentMode.Immediate, W.PresentMode.FifoRelaxed, W.PresentMode.Fifo];
        SW.PresentMode[] silk =
            [SW.PresentMode.Mailbox, SW.PresentMode.Immediate, SW.PresentMode.FifoRelaxed, SW.PresentMode.Fifo];
        for (int mask = 1; mask < 16; mask++)
        {
            W.PresentMode[] advertisedDawn = dawn.Where((_, i) => (mask & (1 << i)) != 0).ToArray();
            SW.PresentMode[] advertisedSilk = silk.Where((_, i) => (mask & (1 << i)) != 0).ToArray();
            W.PresentMode selected = DawnSurfacePresentModePolicy.Select(vsync, advertisedDawn);
            Assert.Contains(selected, advertisedDawn);
            Assert.Equal(WgpuContext.ChoosePresentMode(vsync, advertisedSilk),
                DawnSurfacePresentModePolicy.ToSilk(selected));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingOrInvalidCapabilitiesCannotInventFifoSupport(bool vsync)
    {
        Assert.Throws<NotSupportedException>(() => DawnSurfacePresentModePolicy.Select(vsync, []));
        Assert.Throws<NotSupportedException>(() =>
            DawnSurfacePresentModePolicy.Select(vsync, [(W.PresentMode)0]));
        // Validate the tail even after both preferred modes have appeared.
        Assert.Throws<NotSupportedException>(() =>
            DawnSurfacePresentModePolicy.Select(vsync,
                [W.PresentMode.Immediate, W.PresentMode.Fifo, (W.PresentMode)(-1)]));
    }

    [Fact]
    public void VsyncRequestDoesNotClaimAConfiguredSurface()
    {
        using var context = new WgpuContext();
        Assert.Null(context.ConfiguredPresentMode);
        context.VSync = true;
        Assert.Null(context.ConfiguredPresentMode);
        Assert.False(context.TryConfigureSwapChain(640, 480));
        Assert.Null(context.ConfiguredPresentMode);
    }
}
