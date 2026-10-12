using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using ProGPU.WinUI.Input;
using Xunit;

namespace ProGPU.Tests;

public sealed class RelativePointerCaptureTests
{
    [Fact]
    public void FocusLossReleasesRelativePointerAndStopsMovement()
    {
        var previousState = InputSystem.Current;
        var owner = new Microsoft.UI.Xaml.Controls.Border();
        var state = InputSystem.CreateExternalState(owner);
        var movement = Vector2.Zero;
        var releaseCount = 0;
        try
        {
            InputSystem.Current = state;
            RelativePointerCapture.ConfigureHost(state, () => true, () => releaseCount++);
            Assert.True(RelativePointerCapture.TryAcquire(owner, delta => movement += delta));

            RelativePointerCapture.InjectHostMovement(new Vector2(3f, 4f));
            Assert.Equal(new Vector2(3f, 4f), movement);

            InputSystem.InjectFocusLost();
            Assert.False(RelativePointerCapture.IsCaptured(owner));
            Assert.Equal(1, releaseCount);

            RelativePointerCapture.InjectHostMovement(new Vector2(10f, 10f));
            Assert.Equal(new Vector2(3f, 4f), movement);
        }
        finally
        {
            RelativePointerCapture.ClearHost(state);
            InputSystem.Current = previousState;
        }
    }

    [Fact]
    public void HostInitiatedLossDoesNotRequestSecondPlatformRelease()
    {
        var previousState = InputSystem.Current;
        var owner = new Microsoft.UI.Xaml.Controls.Border();
        var state = InputSystem.CreateExternalState(owner);
        var releaseCount = 0;
        try
        {
            InputSystem.Current = state;
            RelativePointerCapture.ConfigureHost(state, () => true, () => releaseCount++);
            Assert.True(RelativePointerCapture.TryAcquire(owner, _ => { }));

            RelativePointerCapture.NotifyHostCaptureLost();

            Assert.False(RelativePointerCapture.IsCaptured(owner));
            Assert.Equal(0, releaseCount);
        }
        finally
        {
            RelativePointerCapture.ClearHost(state);
            InputSystem.Current = previousState;
        }
    }
}
