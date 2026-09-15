using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProGPU.Backend;

internal static unsafe partial class X11NativeDesktopPointer
{
    private const string Xlib = "libX11.so.6";

    internal static bool TryGetPosition(out NativeWindowPoint position)
    {
        position = default;
        nint display = XOpenDisplay(null);
        if (display == 0)
            return false;

        try
        {
            nuint root = XDefaultRootWindow(display);
            if (root == 0 ||
                XQueryPointer(
                    display,
                    root,
                    out _,
                    out _,
                    out int rootX,
                    out int rootY,
                    out _,
                    out _,
                    out _) == 0)
                return false;

            position = new NativeWindowPoint(rootX, rootY);
            return true;
        }
        finally
        {
            XCloseDisplay(display);
        }
    }

    [LibraryImport(Xlib)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint XOpenDisplay(byte* displayName);

    [LibraryImport(Xlib)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int XCloseDisplay(nint display);

    [LibraryImport(Xlib)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nuint XDefaultRootWindow(nint display);

    [LibraryImport(Xlib)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int XQueryPointer(
        nint display,
        nuint window,
        out nuint root,
        out nuint child,
        out int rootX,
        out int rootY,
        out int windowX,
        out int windowY,
        out uint mask);
}
