using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ProGPU.Backend;

internal static unsafe partial class CocoaNativeDesktopPointer
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    internal static bool TryGetPosition(out NativeWindowPoint position)
    {
        position = default;
        if (pthread_main_np() == 0 ||
            RuntimeInformation.ProcessArchitecture is not (Architecture.Arm64 or Architecture.X64) ||
            typeof(CocoaNativeDesktopPointer).IsCollectible)
            return false;

        nint poolClass = Class("NSAutoreleasePool\0"u8);
        nint screenClass = Class("NSScreen\0"u8);
        nint eventClass = Class("NSEvent\0"u8);
        if (poolClass == 0 || screenClass == 0 || eventClass == 0)
            return false;

        nint pool = Send(Send(poolClass, "alloc\0"u8), "init\0"u8);
        if (pool == 0)
            return false;

        try
        {
            nint primary = Send(Send(screenClass, "screens\0"u8), "firstObject\0"u8);
            if (primary == 0)
                return false;

            nint frameSelector = Selector("frame\0"u8);
            CocoaMenuRect primaryFrame;
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                MessageRectStret(out primaryFrame, primary, frameSelector);
            else
                primaryFrame = MessageRect(primary, frameSelector);

            CocoaMenuPoint nativePoint = MessagePoint(eventClass, Selector("mouseLocation\0"u8));
            return TryMapToDesktop(nativePoint, primaryFrame, out position);
        }
        finally
        {
            MessageVoid(pool, Selector("release\0"u8));
        }
    }

    /// <summary>
    /// The desktop point at the center of <c>NSScreen.mainScreen</c> (the screen with keyboard focus,
    /// else the screen with the menu bar). macOS opens a new window on that screen, not where the
    /// pointer happens to be.
    /// </summary>
    internal static bool TryGetFocusedScreenCenter(out NativeWindowPoint position)
    {
        position = default;
        // Reads screen frames only, so unlike the pointer query it does not need the main thread
        // (the caller creates its window on the UI thread, but tests read it from theirs).
        if (RuntimeInformation.ProcessArchitecture is not (Architecture.Arm64 or Architecture.X64) ||
            typeof(CocoaNativeDesktopPointer).IsCollectible)
            return false;

        // A windowed process already has AppKit; a launcher or test host may not.
        if (!NativeLibrary.TryLoad("/System/Library/Frameworks/AppKit.framework/AppKit", out _))
            return false;

        nint poolClass = Class("NSAutoreleasePool\0"u8);
        nint screenClass = Class("NSScreen\0"u8);
        if (poolClass == 0 || screenClass == 0)
            return false;

        nint pool = Send(Send(poolClass, "alloc\0"u8), "init\0"u8);
        if (pool == 0)
            return false;

        try
        {
            nint screens = Send(screenClass, "screens\0"u8);
            nint primary = Send(screens, "firstObject\0"u8);
            nint main = Send(screenClass, "mainScreen\0"u8);
            if (primary == 0 || main == 0)
                return false;

            nint frameSelector = Selector("frame\0"u8);
            CocoaMenuRect primaryFrame;
            CocoaMenuRect mainFrame;
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                MessageRectStret(out primaryFrame, primary, frameSelector);
                MessageRectStret(out mainFrame, main, frameSelector);
            }
            else
            {
                primaryFrame = MessageRect(primary, frameSelector);
                mainFrame = MessageRect(main, frameSelector);
            }

            return TryMapToDesktop(ScreenCenter(mainFrame), primaryFrame, out position);
        }
        finally
        {
            MessageVoid(pool, Selector("release\0"u8));
        }
    }

    /// <summary>The AppKit bottom-left point at the center of a screen frame.</summary>
    internal static CocoaMenuPoint ScreenCenter(CocoaMenuRect frame) =>
        new(frame.X + frame.Width / 2d, frame.Y + frame.Height / 2d);

    internal static bool TryMapToDesktop(
        CocoaMenuPoint nativePoint,
        CocoaMenuRect primaryFrame,
        out NativeWindowPoint position)
    {
        position = default;
        if (!double.IsFinite(nativePoint.X) || !double.IsFinite(nativePoint.Y) ||
            !double.IsFinite(primaryFrame.X) || !double.IsFinite(primaryFrame.Y) ||
            !double.IsFinite(primaryFrame.Width) || !double.IsFinite(primaryFrame.Height) ||
            primaryFrame.Width <= 0 || primaryFrame.Height <= 0)
            return false;

        double desktopX = nativePoint.X - primaryFrame.X;
        double desktopY = primaryFrame.Y + primaryFrame.Height - nativePoint.Y;
        if (!TryRoundCoordinate(desktopX, out int x) || !TryRoundCoordinate(desktopY, out int y))
            return false;

        position = new NativeWindowPoint(x, y);
        return true;
    }

    private static bool TryRoundCoordinate(double value, out int result)
    {
        result = 0;
        if (!double.IsFinite(value) || value < int.MinValue || value > int.MaxValue)
            return false;
        result = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return true;
    }

    private static nint Class(ReadOnlySpan<byte> name)
    {
        fixed (byte* value = name)
            return objc_getClass(value);
    }

    private static nint Selector(ReadOnlySpan<byte> name)
    {
        fixed (byte* value = name)
            return sel_registerName(value);
    }

    private static nint Send(nint receiver, ReadOnlySpan<byte> selector) =>
        MessageObject(receiver, Selector(selector));

    [LibraryImport("/usr/lib/libSystem.B.dylib")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int pthread_main_np();

    [LibraryImport(ObjC)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint objc_getClass(byte* name);

    [LibraryImport(ObjC)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint sel_registerName(byte* name);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint MessageObject(nint receiver, nint selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial CocoaMenuPoint MessagePoint(nint receiver, nint selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial CocoaMenuRect MessageRect(nint receiver, nint selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend_stret")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void MessageRectStret(out CocoaMenuRect result, nint receiver, nint selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void MessageVoid(nint receiver, nint selector);
}
