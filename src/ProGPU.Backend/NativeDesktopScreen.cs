namespace ProGPU.Backend;

/// <summary>Reads the screen the platform considers current for a new window, in desktop coordinates.</summary>
public static class NativeDesktopScreen
{
    /// <summary>
    /// The center of the screen a newly opened, owner-less window belongs on, in the same desktop
    /// coordinate space as <see cref="NativeDesktopPointer.TryGetPosition"/> and the monitor bounds.
    /// macOS answers with <c>NSScreen.mainScreen</c> (the screen with keyboard focus, else the menu
    /// bar screen), which is where its apps open new windows; Windows and X11 return false so their
    /// own placement rules (the pointer's monitor) stay in force.
    /// </summary>
    public static bool TryGetFocusedScreenCenter(
        NativeWindowKind platformKind,
        out NativeWindowPoint position)
    {
        position = default;
        try
        {
            if (OperatingSystem.IsMacOS() && platformKind == NativeWindowKind.Cocoa)
                return CocoaNativeDesktopPointer.TryGetFocusedScreenCenter(out position);
            return false;
        }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
        catch (BadImageFormatException) { return false; }
    }
}
