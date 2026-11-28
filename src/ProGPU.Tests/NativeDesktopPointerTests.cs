using ProGPU.Backend;
using Xunit;

namespace ProGPU.Tests;

public sealed class NativeDesktopPointerTests
{
    [Fact]
    public void UnsupportedExplicitPlatformDoesNotProbeAnotherWindowSystem()
    {
        Assert.False(NativeDesktopPointer.TryGetPosition(NativeWindowKind.Wayland, out _));
        Assert.False(NativeDesktopPointer.TryGetPosition(NativeWindowKind.Unknown, out _));
    }

    [Theory]
    [InlineData(0, 1080, 0, 0)]
    [InlineData(-1280, 1080, -320, 240)]
    [InlineData(0, 1080, 1919.6, 1079.6)]
    public void CocoaCoordinatesMapToTopLeftDesktop(
        double primaryX,
        double primaryHeight,
        double nativeX,
        double nativeY)
    {
        Assert.True(CocoaNativeDesktopPointer.TryMapToDesktop(
            new CocoaMenuPoint(nativeX, nativeY),
            new CocoaMenuRect(primaryX, 0, 1920, primaryHeight),
            out NativeWindowPoint point));

        Assert.Equal((int)Math.Round(nativeX - primaryX, MidpointRounding.AwayFromZero), point.X);
        Assert.Equal((int)Math.Round(primaryHeight - nativeY, MidpointRounding.AwayFromZero), point.Y);
    }

    [Theory]
    [InlineData(double.NaN, 10, 0, 0, 1920, 1080)]
    [InlineData(10, 10, 0, 0, 0, 1080)]
    [InlineData(10, 10, 0, 0, 1920, -1)]
    [InlineData(2147483648d, 10, 0, 0, 1920, 1080)]
    public void CocoaCoordinatesRejectInvalidDesktopState(
        double nativeX,
        double nativeY,
        double primaryX,
        double primaryY,
        double primaryWidth,
        double primaryHeight)
    {
        Assert.False(CocoaNativeDesktopPointer.TryMapToDesktop(
            new CocoaMenuPoint(nativeX, nativeY),
            new CocoaMenuRect(primaryX, primaryY, primaryWidth, primaryHeight),
            out _));
    }

    [Theory]
    [InlineData(0, 1117, 0, 0, 1728, 1117, 864, 559)]
    [InlineData(0, 1117, 0, 1117, 2560, 1440, 1280, -720)]
    [InlineData(0, 1117, -2560, 0, 2560, 1440, -1280, 397)]
    public void AScreensCenterMapsToTopLeftDesktopCoordinates(
        double primaryX,
        double primaryHeight,
        double screenX,
        double screenY,
        double screenWidth,
        double screenHeight,
        int expectedX,
        int expectedY)
    {
        var screen = new CocoaMenuRect(screenX, screenY, screenWidth, screenHeight);
        var primary = new CocoaMenuRect(primaryX, 0, 1728, primaryHeight);

        Assert.True(CocoaNativeDesktopPointer.TryMapToDesktop(
            CocoaNativeDesktopPointer.ScreenCenter(screen),
            primary,
            out NativeWindowPoint point));
        Assert.Equal((expectedX, expectedY), (point.X, point.Y));
    }

    [Fact]
    public void UnsupportedDesktopScreenPlatformsDoNotGuess()
    {
        Assert.False(NativeDesktopScreen.TryGetFocusedScreenCenter(NativeWindowKind.Wayland, out _));
        Assert.False(NativeDesktopScreen.TryGetFocusedScreenCenter(NativeWindowKind.Unknown, out _));
        Assert.False(NativeDesktopScreen.TryGetFocusedScreenCenter(NativeWindowKind.X11, out _));
    }
}
