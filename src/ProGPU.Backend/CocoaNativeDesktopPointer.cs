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
