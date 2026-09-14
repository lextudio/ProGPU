using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ProGPU.Samples.Suntrail.Presentation;

/// <summary>Shared multi-touch button with a single captured owner and explicit cancellation.</summary>
public sealed class TouchHoldButton(Action<bool> changed) : Button
{
    private uint? _pointer;
    public override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        if (_pointer.HasValue || !IsEnabled) return;
        _pointer = e.Pointer.PointerId; CapturePointer(e.Pointer); Opacity = 1;
        changed(true); App.TouchFeedback?.Invoke(); e.Handled = true;
    }
    public override void OnPointerReleased(PointerRoutedEventArgs e) => Release(e);
    public override void OnPointerCanceled(PointerRoutedEventArgs e) => Release(e);
    public override void OnPointerCaptureLost(PointerRoutedEventArgs e)
    {
        if (_pointer != e.Pointer.PointerId) return;
        _pointer = null; Opacity = .72f; changed(false); e.Handled = true;
    }
    public void Reset() { _pointer = null; ReleasePointerCaptures(); Opacity = .72f; changed(false); }
    private void Release(PointerRoutedEventArgs e)
    {
        if (_pointer != e.Pointer.PointerId) return;
        _pointer = null; ReleasePointerCapture(e.Pointer); Opacity = .72f; changed(false); e.Handled = true;
    }
}
