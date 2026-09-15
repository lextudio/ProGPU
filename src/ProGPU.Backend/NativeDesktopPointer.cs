namespace ProGPU.Backend;

/// <summary>Reads the current pointer position in native desktop coordinates.</summary>
public static class NativeDesktopPointer
{
    /// <summary>
    /// Reads the current pointer position using the platform's global desktop
    /// coordinate space. The result is independent of framebuffer scale and is
    /// suitable for matching <see cref="NativeWindowPoint"/> against monitor bounds.
    /// Wayland deliberately returns <see langword="false"/> because the protocol
    /// does not expose global pointer coordinates.
    /// </summary>
    public static bool TryGetPosition(
        NativeWindowKind platformKind,
        out NativeWindowPoint position)
    {
        position = default;
        try
        {
            if (OperatingSystem.IsWindows() && platformKind == NativeWindowKind.Win32)
                return Win32NativeWindowPlatform.TryGetDesktopPointer(out position);
            if (OperatingSystem.IsMacOS() && platformKind == NativeWindowKind.Cocoa)
                return CocoaNativeDesktopPointer.TryGetPosition(out position);
            if (OperatingSystem.IsLinux() && platformKind == NativeWindowKind.X11)
                return X11NativeDesktopPointer.TryGetPosition(out position);
            return false;
        }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
        catch (BadImageFormatException) { return false; }
    }
}
