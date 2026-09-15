namespace ProGPU.Backend;

internal sealed partial class Win32NativeWindowPlatform
{
    internal static bool TryGetDesktopPointer(out NativeWindowPoint position)
    {
        if (GetCursorPos(out Point cursor) == 0)
        {
            position = default;
            return false;
        }

        position = new NativeWindowPoint(cursor.X, cursor.Y);
        return true;
    }
}
