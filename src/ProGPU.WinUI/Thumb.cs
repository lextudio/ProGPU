using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Numerics;
using ProGPU.Layout;

namespace Microsoft.UI.Xaml.Controls;

public class DragStartedEventArgs : EventArgs
{
    public float HorizontalOffset { get; }
    public float VerticalOffset { get; }

    public DragStartedEventArgs(float horizontalOffset, float verticalOffset)
    {
        HorizontalOffset = horizontalOffset;
        VerticalOffset = verticalOffset;
    }
}

public class DragDeltaEventArgs : EventArgs
{
    public float HorizontalChange { get; }
    public float VerticalChange { get; }

    public DragDeltaEventArgs(float horizontalChange, float verticalChange)
    {
        HorizontalChange = horizontalChange;
        VerticalChange = verticalChange;
    }
}

public class DragCompletedEventArgs : EventArgs
{
    public float HorizontalOffset { get; }
    public float VerticalOffset { get; }
    public bool Canceled { get; }

    public DragCompletedEventArgs(float horizontalOffset, float verticalOffset, bool canceled = false)
    {
        HorizontalOffset = horizontalOffset;
        VerticalOffset = verticalOffset;
        Canceled = canceled;
    }
}

public delegate void DragStartedEventHandler(object sender, DragStartedEventArgs e);
public delegate void DragDeltaEventHandler(object sender, DragDeltaEventArgs e);
public delegate void DragCompletedEventHandler(object sender, DragCompletedEventArgs e);

public class Thumb : Control
{
    public event DragStartedEventHandler? DragStarted;
    public event DragDeltaEventHandler? DragDelta;
    public event DragCompletedEventHandler? DragCompleted;

    private bool _isDragging;
    private Vector2 _startPos;
    private Vector2 _lastPos;

    public Thumb()
    {
        IsTabStop = false;
    }

    public override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        if (IsEnabled)
        {
            _isDragging = true;
            InputSystem.CapturePointer(this);
            _startPos = e.Position;
            _lastPos = e.Position;
            DragStarted?.Invoke(this, new DragStartedEventArgs(e.Position.X, e.Position.Y));
            base.OnPointerPressed(e);
        }
    }

    public override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        if (_isDragging && IsEnabled)
        {
            var delta = e.Position - _lastPos;
            _lastPos = e.Position;
            DragDelta?.Invoke(this, new DragDeltaEventArgs(delta.X, delta.Y));
        }
        base.OnPointerMoved(e);
    }

    public override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            InputSystem.ReleasePointerCapture();
            DragCompleted?.Invoke(this, new DragCompletedEventArgs(e.Position.X, e.Position.Y));
        }
        base.OnPointerReleased(e);
    }
}

