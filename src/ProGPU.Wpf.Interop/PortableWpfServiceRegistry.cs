namespace ProGPU.Wpf.Interop;

public readonly record struct PortableWpfServiceKey(string Name)
{
    public static PortableWpfServiceKey PresentationCore { get; } = new(nameof(PresentationCore));

    public static PortableWpfServiceKey PresentationFramework { get; } = new(nameof(PresentationFramework));

    public static PortableWpfServiceKey WinForms { get; } = new(nameof(WinForms));
}

public interface IPortableClipboardServiceRegistrar
{
    PortableWpfServiceKey ServiceKey { get; }

    IDisposable Register(Func<string?> getText, Action<string?> setText);

    void Clear();
}

public interface IPortableLauncherServiceRegistrar
{
    PortableWpfServiceKey ServiceKey { get; }

    IDisposable Register(Func<PortableLaunchRequest, bool> launch);

    void Clear();
}

public interface IPortableMessageBoxServiceRegistrar
{
    PortableWpfServiceKey ServiceKey { get; }

    IDisposable Register(Func<PortableMessageBoxRequest, string?> show);

    void Clear();
}

public interface IPortableFileDialogServiceRegistrar
{
    PortableWpfServiceKey ServiceKey { get; }

    IDisposable Register(Func<PortableFileDialogRequest, string?> showDialog);

    IDisposable RegisterResult(Func<PortableFileDialogRequest, PortableFileDialogResult?> showDialog)
    {
        ArgumentNullException.ThrowIfNull(showDialog);
        return Register(request => showDialog(request)?.SelectedPath);
    }

    void Clear();
}

public interface IPortableColorDialogServiceRegistrar
{
    PortableWpfServiceKey ServiceKey { get; }

    IDisposable Register(Func<PortableColorDialogRequest, int?> showDialog);

    void Clear();
}

public interface IPortableFontDialogServiceRegistrar
{
    PortableWpfServiceKey ServiceKey { get; }

    IDisposable Register(Func<PortableFontDialogRequest, PortableFontDialogResult?> showDialog);

    void Clear();
}

public interface IPortablePopupServiceRegistrar
{
    PortableWpfServiceKey ServiceKey { get; }

    bool TryCreatePopup(PortablePopupCreateRequest request, out object? presentationSource);

    bool TrySetPopupPosition(object presentationSource, int x, int y);

    bool TrySetPopupSize(object presentationSource, int width, int height);

    bool TryShowPopup(object presentationSource);

    bool TryHidePopup(object presentationSource);

    bool TrySetPopupHitTestable(object presentationSource, bool hitTestable);

    bool TryDestroyPopup(object presentationSource);

    void Clear();
}

public sealed class PortableLaunchRequest
{
    public PortableLaunchRequest(Uri uri, string targetFrame, bool isTopLevel)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        TargetFrame = targetFrame;
        IsTopLevel = isTopLevel;
    }

    public Uri Uri { get; }

    public string TargetFrame { get; }

    public bool IsTopLevel { get; }
}

public sealed class PortableMessageBoxRequest
{
    public PortableMessageBoxRequest(
        string? messageBoxText,
        string? caption,
        string? button,
        string? icon,
        string? defaultResult,
        string? options,
        string? fallbackResult)
        : this(null, messageBoxText, caption, button, icon, defaultResult, options, fallbackResult)
    {
    }

    public PortableMessageBoxRequest(
        object? owner,
        string? messageBoxText,
        string? caption,
        string? button,
        string? icon,
        string? defaultResult,
        string? options,
        string? fallbackResult)
    {
        Owner = owner;
        MessageBoxText = messageBoxText ?? string.Empty;
        Caption = caption ?? string.Empty;
        Button = button ?? "OK";
        Icon = icon ?? "None";
        DefaultResult = defaultResult ?? "None";
        Options = options ?? "None";
        FallbackResult = fallbackResult ?? "OK";
    }

    public object? Owner { get; }

    public string MessageBoxText { get; }

    public string Caption { get; }

    public string Button { get; }

    public string Icon { get; }

    public string DefaultResult { get; }

    public string Options { get; }

    public string FallbackResult { get; }
}

public sealed class PortableFileDialogRequest
{
    public PortableFileDialogRequest(
        string? kind,
        string? title,
        string? initialDirectory,
        string? defaultDirectory,
        string? suggestedItemName,
        string? defaultExtension,
        string? filter,
        int filterIndex)
        : this(
            kind,
            title,
            initialDirectory,
            defaultDirectory,
            suggestedItemName,
            defaultExtension,
            filter,
            filterIndex,
            allowMultipleSelection: false)
    {
    }

    public PortableFileDialogRequest(
        string? kind,
        string? title,
        string? initialDirectory,
        string? defaultDirectory,
        string? suggestedItemName,
        string? defaultExtension,
        string? filter,
        int filterIndex,
        bool allowMultipleSelection)
    {
        Kind = kind ?? "OpenFile";
        Title = title ?? string.Empty;
        InitialDirectory = initialDirectory ?? string.Empty;
        DefaultDirectory = defaultDirectory ?? string.Empty;
        SuggestedItemName = suggestedItemName ?? string.Empty;
        DefaultExtension = defaultExtension ?? string.Empty;
        Filter = filter ?? string.Empty;
        FilterIndex = filterIndex;
        AllowMultipleSelection = allowMultipleSelection;
    }

    public string Kind { get; }

    public string Title { get; }

    public string InitialDirectory { get; }

    public string DefaultDirectory { get; }

    public string SuggestedItemName { get; }

    public string DefaultExtension { get; }

    public string Filter { get; }

    public int FilterIndex { get; }

    public bool AllowMultipleSelection { get; }
}

public sealed class PortableFileDialogResult
{
    private readonly string[] _selectedPaths;

    public PortableFileDialogResult(string selectedPath)
    {
        ArgumentNullException.ThrowIfNull(selectedPath);
        _selectedPaths = [selectedPath];
    }

    public PortableFileDialogResult(ReadOnlySpan<string> selectedPaths)
    {
        _selectedPaths = selectedPaths.ToArray();
    }

    public int SelectedPathCount => _selectedPaths.Length;

    public string? SelectedPath => _selectedPaths.Length == 0 ? null : _selectedPaths[0];

    public ReadOnlySpan<string> SelectedPaths => _selectedPaths;

    public string GetSelectedPath(int index)
    {
        return _selectedPaths[index];
    }

    public string[] ToArray()
    {
        return (string[])_selectedPaths.Clone();
    }
}

public sealed class PortableColorDialogRequest
{
    private readonly int[] _customColors;

    public PortableColorDialogRequest(int initialArgb, IReadOnlyList<int>? customColors)
    {
        InitialArgb = initialArgb;

        if (customColors == null || customColors.Count == 0)
        {
            _customColors = Array.Empty<int>();
        }
        else
        {
            _customColors = new int[customColors.Count];
            for (var i = 0; i < customColors.Count; i++)
            {
                _customColors[i] = customColors[i];
            }
        }
    }

    public int InitialArgb { get; }

    public IReadOnlyList<int> CustomColors => _customColors;
}

public sealed class PortableFontDialogRequest
{
    public PortableFontDialogRequest(
        string? familyName,
        float size,
        int style,
        string? unit,
        bool showEffects,
        bool showColor,
        int minSize,
        int maxSize)
    {
        FamilyName = string.IsNullOrWhiteSpace(familyName) ? "Courier New" : familyName!;
        Size = size > 0 && float.IsFinite(size) ? size : 10f;
        Style = style;
        Unit = string.IsNullOrWhiteSpace(unit) ? "Point" : unit!;
        ShowEffects = showEffects;
        ShowColor = showColor;
        MinSize = minSize;
        MaxSize = maxSize;
    }

    public string FamilyName { get; }

    public float Size { get; }

    public int Style { get; }

    public string Unit { get; }

    public bool ShowEffects { get; }

    public bool ShowColor { get; }

    public int MinSize { get; }

    public int MaxSize { get; }
}

public sealed class PortableFontDialogResult
{
    public PortableFontDialogResult(string? familyName, float size, int style, string? unit)
    {
        FamilyName = string.IsNullOrWhiteSpace(familyName) ? "Courier New" : familyName!;
        Size = size > 0 && float.IsFinite(size) ? size : 10f;
        Style = style;
        Unit = string.IsNullOrWhiteSpace(unit) ? "Point" : unit!;
    }

    public string FamilyName { get; }

    public float Size { get; }

    public int Style { get; }

    public string Unit { get; }
}

public sealed class PortablePopupCreateRequest
{
    public PortablePopupCreateRequest(
        object? placementTarget,
        object? ownerPresentationSource,
        IntPtr ownerHandle,
        int x,
        int y,
        bool isTransparent,
        bool isChildPopup)
    {
        PlacementTarget = placementTarget;
        OwnerPresentationSource = ownerPresentationSource;
        OwnerHandle = ownerHandle;
        X = x;
        Y = y;
        IsTransparent = isTransparent;
        IsChildPopup = isChildPopup;
    }

    public object? PlacementTarget { get; }

    public object? OwnerPresentationSource { get; }

    public IntPtr OwnerHandle { get; }

    public int X { get; }

    public int Y { get; }

    public bool IsTransparent { get; }

    public bool IsChildPopup { get; }
}

public sealed class PortableWindowRegion
{
    public PortableWindowRegion(PortableRect bounds, IReadOnlyList<PortableRect>? excludedRects = null)
    {
        Bounds = bounds;
        ExcludedRects = excludedRects ?? Array.Empty<PortableRect>();
    }

    public PortableRect Bounds { get; }

    public IReadOnlyList<PortableRect> ExcludedRects { get; }

    public bool IsEmpty => Bounds.IsEmpty || Bounds.Width <= 0 || Bounds.Height <= 0;
}

public sealed class PortableWindowActivationCallbacks
{
    public PortableWindowActivationCallbacks(
        Func<object, object?> activate,
        Action<object>? show = null,
        Action<object>? hide = null,
        Action<object, object>? setWindowState = null,
        Action<object, string>? setTitle = null,
        Action<object, double, double>? setClientSize = null,
        Action<object, double, double>? setPosition = null,
        Action<object, bool>? setTopmost = null,
        Action<object, object, object>? setWindowBorder = null,
        Action<object>? close = null,
        Action<object>? run = null,
        Action<object>? dispose = null,
        Func<object, bool>? dragMove = null,
        Func<object, IntPtr>? getHandle = null,
        Func<IntPtr, PortableWindowRegion, bool>? setWindowRegion = null,
        Action<object>? requestDispatcherProcessing = null,
        Action? requestSynchronousPump = null)
    {
        Activate = activate ?? throw new ArgumentNullException(nameof(activate));
        Show = show;
        Hide = hide;
        SetWindowState = setWindowState;
        SetTitle = setTitle;
        SetClientSize = setClientSize;
        SetPosition = setPosition;
        SetTopmost = setTopmost;
        SetWindowBorder = setWindowBorder;
        Close = close;
        Run = run;
        Dispose = dispose;
        DragMove = dragMove;
        GetHandle = getHandle;
        SetWindowRegion = setWindowRegion;
        RequestDispatcherProcessing = requestDispatcherProcessing;
        RequestSynchronousPump = requestSynchronousPump;
    }

    public Func<object, object?> Activate { get; }

    public Action<object>? Show { get; }

    public Action<object>? Hide { get; }

    public Action<object, object>? SetWindowState { get; }

    public Action<object, string>? SetTitle { get; }

    public Action<object, double, double>? SetClientSize { get; }

    public Action<object, double, double>? SetPosition { get; }

    public Action<object, bool>? SetTopmost { get; }

    public Action<object, object, object>? SetWindowBorder { get; }

    public Action<object>? Close { get; }

    public Action<object>? Run { get; }

    public Action<object>? Dispose { get; }

    public Func<object, bool>? DragMove { get; }

    public Func<object, IntPtr>? GetHandle { get; }

    public Func<IntPtr, PortableWindowRegion, bool>? SetWindowRegion { get; }

    public Action<object>? RequestDispatcherProcessing { get; }

    /// <summary>
    /// Deliberately separate from <see cref="RequestDispatcherProcessing"/>, which fires on every
    /// dispatcher operation posted and must stay a cheap fire-and-forget wake. This one backs
    /// exactly one caller on the WPF side - Window's modal ShowDialog wait loop - which needs to
    /// synchronously pump one native event tick, on the same thread, because nothing else will
    /// while it's "blocked" with no OS message queue to wait on. Takes no argument: unlike the
    /// per-window callbacks above, a single global handler is enough, since pumping is inherently
    /// global (every active native window gets ticked, not just one).
    /// </summary>
    public Action? RequestSynchronousPump { get; }
}

public sealed class PortablePopupActivationCallbacks
{
    public PortablePopupActivationCallbacks(
        Func<double, double, bool, bool, object?> create,
        Func<object, object?>? getPresentationSource = null,
        Action<object>? show = null,
        Action<object>? hide = null,
        Action<object, bool, double, double, bool, double, double>? setPosition = null,
        Action<object>? dispose = null,
        GetScreenOriginCallback? getScreenOrigin = null,
        GetMonitorBoundsCallback? getMonitorBounds = null)
    {
        Create = create ?? throw new ArgumentNullException(nameof(create));
        GetPresentationSource = getPresentationSource;
        Show = show;
        Hide = hide;
        SetPosition = setPosition;
        Dispose = dispose;
        GetScreenOrigin = getScreenOrigin;
        GetMonitorBounds = getMonitorBounds;
    }

    /// <summary>(x, y, transparent, useSharedWindow) -&gt; opaque activation handle, or null on failure.</summary>
    public Func<double, double, bool, bool, object?> Create { get; }

    /// <summary>Extracts the bound (portable) PresentationSource from an activation handle.</summary>
    public Func<object, object?>? GetPresentationSource { get; }

    public Action<object>? Show { get; }

    public Action<object>? Hide { get; }

    /// <summary>(activation, position, x, y, size, width, height).</summary>
    public Action<object, bool, double, double, bool, double, double>? SetPosition { get; }

    public Action<object>? Dispose { get; }

    /// <summary>Resolves the logical/DIP screen origin of the window hosting the given presentation source.</summary>
    public GetScreenOriginCallback? GetScreenOrigin { get; }

    /// <summary>Resolves the monitor bounds (device pixels) that contain the given screen-space point.</summary>
    public GetMonitorBoundsCallback? GetMonitorBounds { get; }
}

/// <summary>Resolves the logical/DIP screen origin (x, y) of the window hosting the given presentation source.</summary>
public delegate bool GetScreenOriginCallback(object presentationSource, out double x, out double y);

/// <summary>Resolves the monitor bounds (left, top, width, height) in device pixels for the monitor
/// that contains the given screen-space point.  Returns false when the platform cannot determine
/// monitor geometry, in which case the caller falls back to default primary-screen heuristics.</summary>
public delegate bool GetMonitorBoundsCallback(double screenX, double screenY, out double left, out double top, out double width, out double height);

public interface IPortablePopupActivationServiceRegistrar
{
    PortableWpfServiceKey ServiceKey { get; }

    void Register(PortablePopupActivationCallbacks callbacks);

    void Clear();
}

/// <summary>
/// Portable substitute for the OS-level guarantees a menu overlay needs on Win32 for free
/// (WS_EX_NOACTIVATE/TOPMOST z-order, a single system-wide input redirect while a menu holds
/// capture). PresentationFramework calls <see cref="Register"/> once at startup with the
/// enter/exit callbacks for the shared transparent overlay window; it invokes them as
/// <c>MenuBase.IsMenuMode</c> flips true/false. See librewpf/docs/menus.md, "Proposed redesign:
/// one shared transparent overlay for all transient popups."
/// </summary>
public interface IPortableMenuOverlayServiceRegistrar
{
    PortableWpfServiceKey ServiceKey { get; }

    /// <summary>
    /// The enter callback receives the logical/DIP screen position of the menu's owning window
    /// (<c>ownerScreenX</c>, <c>ownerScreenY</c>) so the overlay can be scoped to just the monitor
    /// that window lives on. A single native window cannot span multiple monitors on this backend
    /// (GLFW/macOS), so a full-virtual-desktop overlay would land on the wrong display; the owner's
    /// position picks the correct one.
    /// <paramref name="getPresentationSource"/> returns the shared overlay's bound (portable)
    /// PresentationSource while menu-mode is active, or null if no overlay currently exists.
    /// <paramref name="getScreenOrigin"/> resolves that same source's logical/DIP screen origin
    /// (reusing the same host-registration lookup per-popup windows already register into), so a
    /// popup being attached windowlessly onto the overlay can compute the local offset needed to
    /// land at its intended absolute screen position.
    /// </summary>
    void Register(
        Action<double, double> enterMenuMode,
        Action exitMenuMode,
        Func<object?> getPresentationSource,
        GetScreenOriginCallback getScreenOrigin);

    void Clear();
}

public sealed class PortableWindowInputEvent
{
    public PortableWindowInputEvent(
        int kind,
        string? key = null,
        int scanCode = 0,
        char? character = null,
        double x = 0,
        double y = 0,
        double deltaX = 0,
        double deltaY = 0,
        int button = 0,
        int modifiers = 0)
    {
        Kind = kind;
        Key = key;
        ScanCode = scanCode;
        Character = character;
        X = x;
        Y = y;
        DeltaX = deltaX;
        DeltaY = deltaY;
        Button = button;
        Modifiers = modifiers;
    }

    public int Kind { get; }

    public string? Key { get; }

    public int ScanCode { get; }

    public char? Character { get; }

    public double X { get; }

    public double Y { get; }

    public double DeltaX { get; }

    public double DeltaY { get; }

    public int Button { get; }

    public int Modifiers { get; }

    public bool Handled { get; set; }
}

public enum PortableWindowCloseResult
{
    NotInvoked = 0,
    Closed = 1,
    Canceled = 2
}

public interface IPortableWindowActivationServiceRegistrar
{
    PortableWpfServiceKey ServiceKey { get; }

    void Register(PortableWindowActivationCallbacks callbacks);

    bool TryRegisterMediaContextRenderService(
        object window,
        Action<object?, TimeSpan> requestRender,
        out IDisposable? registration)
    {
        registration = null;
        return false;
    }

    bool TryIsCurrentApplicationMainWindow(object window, out bool isMainWindow);

    bool TryCloseWindow(object window, out PortableWindowCloseResult result);

    bool TryIsWindowDisposed(object window, out bool isDisposed)
    {
        isDisposed = false;
        return false;
    }

    bool TrySetActivationState(object window, bool isActive);

    // Portable substitute for WM_MOVE: real Win32 WPF never needs this notification because
    // popup HWNDs are owned/child windows that the OS repositions automatically when the
    // owner moves. Portable popups get their own independent native window, so nothing tells
    // WPF the owner moved unless we synthesize it here.
    bool TryNotifyWindowMoved(object window, double x, double y)
    {
        return false;
    }

    bool TryBeginInvokeInput(object window, Action callback);

    bool TryProcessInputEvent(object window, PortableWindowInputEvent input);

    bool TryProcessPresentationSourceInputEvent(object presentationSource, PortableWindowInputEvent input)
    {
        return false;
    }

    bool TryFlushDispatcherOperations(object window, string markerPriorityName, TimeSpan? timeout);

    bool TryPromoteDispatcherTimers(object window, int currentTimeInTicks)
    {
        return false;
    }

    bool TrySetWindowRegion(IntPtr handle, PortableWindowRegion region)
    {
        return false;
    }

    bool TryProcessDragDropEvent(
        object window,
        int dragDropEventKind,
        string[] files,
        string? text,
        double x,
        double y,
        int allowedEffects,
        int acceptedEffect,
        out int result);

    void Clear();
}

public static class PortableWpfServiceRegistry
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<PortableWpfServiceKey, IPortableWindowActivationServiceRegistrar> WindowActivationServices = new();
    private static readonly Dictionary<PortableWpfServiceKey, IPortablePopupActivationServiceRegistrar> PopupActivationServices = new();
    private static readonly Dictionary<PortableWpfServiceKey, IPortableMenuOverlayServiceRegistrar> MenuOverlayServices = new();
    private static readonly Dictionary<PortableWpfServiceKey, IPortableClipboardServiceRegistrar> ClipboardServices = new();
    private static readonly Dictionary<PortableWpfServiceKey, IPortableLauncherServiceRegistrar> LauncherServices = new();
    private static readonly Dictionary<PortableWpfServiceKey, IPortableMessageBoxServiceRegistrar> MessageBoxServices = new();
    private static readonly Dictionary<PortableWpfServiceKey, IPortableFileDialogServiceRegistrar> FileDialogServices = new();
    private static readonly Dictionary<PortableWpfServiceKey, IPortableColorDialogServiceRegistrar> ColorDialogServices = new();
    private static readonly Dictionary<PortableWpfServiceKey, IPortableFontDialogServiceRegistrar> FontDialogServices = new();
    private static readonly Dictionary<PortableWpfServiceKey, IPortablePopupServiceRegistrar> PopupServices = new();

    public static event Action<IPortableClipboardServiceRegistrar>? ClipboardServiceRegistered;

    public static event Action<IPortableMessageBoxServiceRegistrar>? MessageBoxServiceRegistered;

    public static event Action<IPortableFileDialogServiceRegistrar>? FileDialogServiceRegistered;

    public static event Action<IPortableColorDialogServiceRegistrar>? ColorDialogServiceRegistered;

    public static event Action<IPortableFontDialogServiceRegistrar>? FontDialogServiceRegistered;

    public static IDisposable RegisterWindowActivationService(IPortableWindowActivationServiceRegistrar service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ValidateServiceKey(service.ServiceKey, nameof(service));

        lock (SyncRoot)
        {
            WindowActivationServices[service.ServiceKey] = service;
        }

        return new Registration<IPortableWindowActivationServiceRegistrar>(service, WindowActivationServices);
    }

    public static bool TryGetWindowActivationService(
        PortableWpfServiceKey serviceKey,
        out IPortableWindowActivationServiceRegistrar service)
    {
        ValidateServiceKey(serviceKey, nameof(serviceKey));

        lock (SyncRoot)
        {
            return WindowActivationServices.TryGetValue(serviceKey, out service!);
        }
    }

    public static IDisposable RegisterPopupActivationService(IPortablePopupActivationServiceRegistrar service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ValidateServiceKey(service.ServiceKey, nameof(service));

        lock (SyncRoot)
        {
            PopupActivationServices[service.ServiceKey] = service;
        }

        return new Registration<IPortablePopupActivationServiceRegistrar>(service, PopupActivationServices);
    }

    public static bool TryGetPopupActivationService(
        PortableWpfServiceKey serviceKey,
        out IPortablePopupActivationServiceRegistrar service)
    {
        ValidateServiceKey(serviceKey, nameof(serviceKey));

        lock (SyncRoot)
        {
            return PopupActivationServices.TryGetValue(serviceKey, out service!);
        }
    }

    public static IDisposable RegisterMenuOverlayService(IPortableMenuOverlayServiceRegistrar service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ValidateServiceKey(service.ServiceKey, nameof(service));

        lock (SyncRoot)
        {
            MenuOverlayServices[service.ServiceKey] = service;
        }

        return new Registration<IPortableMenuOverlayServiceRegistrar>(service, MenuOverlayServices);
    }

    public static bool TryGetMenuOverlayService(
        PortableWpfServiceKey serviceKey,
        out IPortableMenuOverlayServiceRegistrar service)
    {
        ValidateServiceKey(serviceKey, nameof(serviceKey));

        lock (SyncRoot)
        {
            return MenuOverlayServices.TryGetValue(serviceKey, out service!);
        }
    }

    public static IDisposable RegisterClipboardService(IPortableClipboardServiceRegistrar service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ValidateServiceKey(service.ServiceKey, nameof(service));

        lock (SyncRoot)
        {
            ClipboardServices[service.ServiceKey] = service;
        }

        ClipboardServiceRegistered?.Invoke(service);
        return new Registration<IPortableClipboardServiceRegistrar>(service, ClipboardServices);
    }

    public static bool TryGetClipboardService(
        PortableWpfServiceKey serviceKey,
        out IPortableClipboardServiceRegistrar service)
    {
        ValidateServiceKey(serviceKey, nameof(serviceKey));

        lock (SyncRoot)
        {
            return ClipboardServices.TryGetValue(serviceKey, out service!);
        }
    }

    public static IDisposable RegisterLauncherService(IPortableLauncherServiceRegistrar service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ValidateServiceKey(service.ServiceKey, nameof(service));

        lock (SyncRoot)
        {
            LauncherServices[service.ServiceKey] = service;
        }

        return new Registration<IPortableLauncherServiceRegistrar>(service, LauncherServices);
    }

    public static bool TryGetLauncherService(
        PortableWpfServiceKey serviceKey,
        out IPortableLauncherServiceRegistrar service)
    {
        ValidateServiceKey(serviceKey, nameof(serviceKey));

        lock (SyncRoot)
        {
            return LauncherServices.TryGetValue(serviceKey, out service!);
        }
    }

    public static IDisposable RegisterMessageBoxService(IPortableMessageBoxServiceRegistrar service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ValidateServiceKey(service.ServiceKey, nameof(service));

        lock (SyncRoot)
        {
            MessageBoxServices[service.ServiceKey] = service;
        }

        MessageBoxServiceRegistered?.Invoke(service);
        return new Registration<IPortableMessageBoxServiceRegistrar>(service, MessageBoxServices);
    }

    public static bool TryGetMessageBoxService(
        PortableWpfServiceKey serviceKey,
        out IPortableMessageBoxServiceRegistrar service)
    {
        ValidateServiceKey(serviceKey, nameof(serviceKey));

        lock (SyncRoot)
        {
            return MessageBoxServices.TryGetValue(serviceKey, out service!);
        }
    }

    public static IDisposable RegisterFileDialogService(IPortableFileDialogServiceRegistrar service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ValidateServiceKey(service.ServiceKey, nameof(service));

        lock (SyncRoot)
        {
            FileDialogServices[service.ServiceKey] = service;
        }

        FileDialogServiceRegistered?.Invoke(service);
        return new Registration<IPortableFileDialogServiceRegistrar>(service, FileDialogServices);
    }

    public static bool TryGetFileDialogService(
        PortableWpfServiceKey serviceKey,
        out IPortableFileDialogServiceRegistrar service)
    {
        ValidateServiceKey(serviceKey, nameof(serviceKey));

        lock (SyncRoot)
        {
            return FileDialogServices.TryGetValue(serviceKey, out service!);
        }
    }

    public static IDisposable RegisterColorDialogService(IPortableColorDialogServiceRegistrar service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ValidateServiceKey(service.ServiceKey, nameof(service));

        lock (SyncRoot)
        {
            ColorDialogServices[service.ServiceKey] = service;
        }

        ColorDialogServiceRegistered?.Invoke(service);
        return new Registration<IPortableColorDialogServiceRegistrar>(service, ColorDialogServices);
    }

    public static bool TryGetColorDialogService(
        PortableWpfServiceKey serviceKey,
        out IPortableColorDialogServiceRegistrar service)
    {
        ValidateServiceKey(serviceKey, nameof(serviceKey));

        lock (SyncRoot)
        {
            return ColorDialogServices.TryGetValue(serviceKey, out service!);
        }
    }

    public static IDisposable RegisterFontDialogService(IPortableFontDialogServiceRegistrar service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ValidateServiceKey(service.ServiceKey, nameof(service));

        lock (SyncRoot)
        {
            FontDialogServices[service.ServiceKey] = service;
        }

        FontDialogServiceRegistered?.Invoke(service);
        return new Registration<IPortableFontDialogServiceRegistrar>(service, FontDialogServices);
    }

    public static bool TryGetFontDialogService(
        PortableWpfServiceKey serviceKey,
        out IPortableFontDialogServiceRegistrar service)
    {
        ValidateServiceKey(serviceKey, nameof(serviceKey));

        lock (SyncRoot)
        {
            return FontDialogServices.TryGetValue(serviceKey, out service!);
        }
    }

    public static IDisposable RegisterPopupService(IPortablePopupServiceRegistrar service)
    {
        ArgumentNullException.ThrowIfNull(service);
        ValidateServiceKey(service.ServiceKey, nameof(service));

        lock (SyncRoot)
        {
            PopupServices[service.ServiceKey] = service;
        }

        return new Registration<IPortablePopupServiceRegistrar>(service, PopupServices);
    }

    public static bool TryGetPopupService(
        PortableWpfServiceKey serviceKey,
        out IPortablePopupServiceRegistrar service)
    {
        ValidateServiceKey(serviceKey, nameof(serviceKey));

        lock (SyncRoot)
        {
            return PopupServices.TryGetValue(serviceKey, out service!);
        }
    }

    private static void ValidateServiceKey(PortableWpfServiceKey serviceKey, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(serviceKey.Name))
        {
            throw new ArgumentException("Portable WPF service keys must have a non-empty name.", parameterName);
        }
    }

    private sealed class Registration<TService> : IDisposable
        where TService : class
    {
        private readonly Dictionary<PortableWpfServiceKey, TService> _services;
        private TService? _service;

        public Registration(TService service, Dictionary<PortableWpfServiceKey, TService> services)
        {
            _service = service;
            _services = services;
        }

        public void Dispose()
        {
            var service = _service;
            if (service == null)
            {
                return;
            }

            _service = null;

            lock (SyncRoot)
            {
                var serviceKey = GetServiceKey(service);
                if (_services.TryGetValue(serviceKey, out var current) &&
                    ReferenceEquals(current, service))
                {
                    _services.Remove(serviceKey);
                }
            }
        }

        private static PortableWpfServiceKey GetServiceKey(TService service)
        {
            return service switch
            {
                IPortableWindowActivationServiceRegistrar windowActivationService => windowActivationService.ServiceKey,
                IPortablePopupActivationServiceRegistrar popupActivationService => popupActivationService.ServiceKey,
                IPortableMenuOverlayServiceRegistrar menuOverlayService => menuOverlayService.ServiceKey,
                IPortableClipboardServiceRegistrar clipboardService => clipboardService.ServiceKey,
                IPortableLauncherServiceRegistrar launcherService => launcherService.ServiceKey,
                IPortableMessageBoxServiceRegistrar messageBoxService => messageBoxService.ServiceKey,
                IPortableFileDialogServiceRegistrar fileDialogService => fileDialogService.ServiceKey,
                IPortableColorDialogServiceRegistrar colorDialogService => colorDialogService.ServiceKey,
                IPortableFontDialogServiceRegistrar fontDialogService => fontDialogService.ServiceKey,
                IPortablePopupServiceRegistrar popupService => popupService.ServiceKey,
                _ => throw new InvalidOperationException("Unsupported portable WPF service registrar.")
            };
        }
    }
}
