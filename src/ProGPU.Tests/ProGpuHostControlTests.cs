using System.Reflection;
using ProGPU.Avalonia;
using Xunit;

namespace ProGPU.Tests;

public class ProGpuHostControlTests
{
    [Fact]
    public void ZeroCopyCompositionIsOptInByDefault()
    {
        var control = new ProGpuHostControl();

        Assert.False(control.EnableZeroCopy);
    }

    [Fact]
    public void HostControlCanFallbackWhenSharedImageImportFails()
    {
        var resizeMethod = typeof(ProGpuHostControl).GetMethod(
            "ResizeSharedResources",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var fallbackMethod = typeof(ProGpuHostControl).GetMethod(
            "TryUseCustomVisualFallback",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(resizeMethod);
        Assert.Equal(typeof(bool), resizeMethod.ReturnType);
        Assert.NotNull(fallbackMethod);
        Assert.Equal(typeof(bool), fallbackMethod.ReturnType);
    }
}
