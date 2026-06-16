using ProGPU.Backend;
using Silk.NET.WebGPU;
using System.Reflection;
using Xunit;

namespace ProGPU.Tests;

public sealed class WgpuContextTests
{
    [Fact]
    public void VsyncOffUsesImmediateWhenSurfaceAdvertisesIt()
    {
        var selected = WgpuContext.ChoosePresentMode(
            vsync: false,
            [PresentMode.Fifo, PresentMode.Immediate]);

        Assert.Equal(PresentMode.Immediate, selected);
    }

    [Fact]
    public void VsyncOffFallsBackToAdvertisedPresentModeWhenImmediateIsAbsent()
    {
        var selected = WgpuContext.ChoosePresentMode(
            vsync: false,
            [PresentMode.Fifo]);

        Assert.Equal(PresentMode.Fifo, selected);
    }

    [Fact]
    public void VsyncOnPrefersFifoWhenSurfaceAdvertisesIt()
    {
        var selected = WgpuContext.ChoosePresentMode(
            vsync: true,
            [PresentMode.Immediate, PresentMode.Fifo]);

        Assert.Equal(PresentMode.Fifo, selected);
    }

    [Theory]
    [InlineData(15, 16u, 16u, 4u, true)]
    [InlineData(16, 16u, 16u, 4u, false)]
    [InlineData(16, 17u, 17u, 4u, true)]
    [InlineData(16, 17u, 16u, 4u, false)]
    [InlineData(16, 17u, 17u, 3u, false)]
    public void WpfShaderEffectMaskBindingFollowsDeviceLimits(
        int activeSamplerRegisterCount,
        uint maxSampledTexturesPerShaderStage,
        uint maxSamplersPerShaderStage,
        uint maxBindGroups,
        bool expected)
    {
        var canBind = WgpuContext.CanBindWpfShaderEffectMask(
            activeSamplerRegisterCount,
            maxSampledTexturesPerShaderStage,
            maxSamplersPerShaderStage,
            maxBindGroups);

        Assert.Equal(expected, canBind);
    }

    [Fact]
    public void PendingResourceSnapshotDropsDuplicateAndZeroPointers()
    {
        var method = typeof(WgpuContext).GetMethod(
            "SnapshotPendingResourcePointers",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var pending = new List<IntPtr>
        {
            new(1),
            new(2),
            IntPtr.Zero,
            new(1),
            new(3),
            new(2)
        };

        var snapshot = Assert.IsType<IntPtr[]>(method.Invoke(null, new object[] { pending }));

        Assert.Equal(new[] { new IntPtr(1), new IntPtr(2), new IntPtr(3) }, snapshot);
    }
}
