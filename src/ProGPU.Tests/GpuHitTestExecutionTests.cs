using ProGPU.Backend;
using Xunit;

namespace ProGPU.Tests;

public sealed class GpuHitTestExecutionTests
{
    private static readonly WgpuComputeLimits Limits = new(128UL * 1024 * 1024, 8, 256, 256, 65535);

    [Fact]
    public void AutomaticRetainsSinglePassUntilQualification()
    {
        Assert.Equal(GpuHitTestExecutionPreference.SinglePass, GpuHitTestExecutionPolicy.Resolve(GpuHitTestExecutionPreference.Automatic));
        Assert.Equal(GpuHitTestExecutionPreference.OrderedStages, GpuHitTestExecutionPolicy.Resolve(GpuHitTestExecutionPreference.OrderedStages));
        Assert.Throws<ArgumentOutOfRangeException>(() => GpuHitTestExecutionPolicy.Resolve((GpuHitTestExecutionPreference)99));
    }

    [Theory]
    [InlineData(1u, true)]
    [InlineData(64u, true)]
    [InlineData(65u, true)]
    [InlineData(4194240u, true)]
    [InlineData(4194241u, false)]
    [InlineData(0u, false)]
    [InlineData(uint.MaxValue, false)]
    public void DispatchBoundsAreCheckedWithoutIntegerWrap(uint count, bool admitted) =>
        Assert.Equal(admitted, Limits.AdmitsOrderedHitQuery(count, WgpuContext.DefaultMaxBufferSize));

    [Fact]
    public void MissingOrInsufficientDeviceLimitsReject()
    {
        Assert.False(default(WgpuComputeLimits).AdmitsOrderedHitQuery(1, 256));
        Assert.False(Limits.AdmitsOrderedHitQuery(1, 39));
        Assert.True(Limits.AdmitsOrderedHitQuery(1, 40));
        Assert.False((Limits with { MaxStorageBufferBindingSize = 39 }).AdmitsOrderedHitQuery(1, 256));
        Assert.False((Limits with { MaxStorageBuffersPerShaderStage = 6 }).AdmitsOrderedHitQuery(1, 256));
        Assert.False((Limits with { MaxComputeInvocationsPerWorkgroup = 63 }).AdmitsOrderedHitQuery(1, 256));
        Assert.False((Limits with { MaxComputeWorkgroupSizeX = 63 }).AdmitsOrderedHitQuery(1, 256));
        Assert.False((Limits with { MaxComputeWorkgroupsPerDimension = 0 }).AdmitsOrderedHitQuery(1, 256));
    }
}
