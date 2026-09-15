# Native desktop pointer position

`NativeDesktopPointer.TryGetPosition` is the typed ProGPU boundary for reading
the current pointer in native desktop coordinates before a top-level window
exists. Callers must supply the selected `NativeWindowKind`; the provider never
guesses between X11 and Wayland and never treats one window system's coordinates
as another's.

The supported desktop providers are:

- Win32 uses the operating system cursor position in screen coordinates.
- Cocoa reads AppKit's global mouse point on the main thread and maps its
  bottom-left screen convention into the same top-left desktop convention used
  by ProGPU window placement. Coordinates remain points, not backing pixels.
- X11 opens one short-lived display connection, queries the root pointer, and
  closes that connection before returning. It does not mutate GLFW's event
  connection.
- Wayland and unknown platforms return `false`; the Wayland protocol does not
  expose a global desktop pointer coordinate.

The call performs no GPU work and retains no native handle. Missing libraries,
entry points, invalid Cocoa screen metadata, unavailable displays, unsupported
architectures, and non-main-thread Cocoa calls fail closed. A caller may then
choose an explicit policy such as the primary monitor; ProGPU does not fabricate
a pointer point.

The first consumer is LibreWPF `WindowStartupLocation.CenterScreen`. Its monitor
service resolves the same Linux window-system preference that GLFW will use,
then streams the returned point through ProGPU's existing nearest-monitor
selection. Owned centering continues to select the owner's monitor and never
uses the pointer.

Compilation coverage builds `ProGPU.Backend` and `ProGPU.Tests` under the normal
warnings-as-errors graph. Unit fixtures cover Cocoa coordinate conversion,
finite/range rejection, and explicit unsupported platform dispatch. Final
qualification still requires a multi-monitor Win32/Cocoa/X11 application run;
Wayland must retain the documented primary-monitor fallback rather than being
reported as global-position parity.
