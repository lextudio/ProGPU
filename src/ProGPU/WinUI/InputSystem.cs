using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.Input;
using ProGPU.Scene;
using ProGPU.Vector;

namespace ProGPU.WinUI;

public static class InputSystem
{
    private static FrameworkElement? _root;
    private static FrameworkElement? _hoveredElement;
    private static FrameworkElement? _focusedElement;
    private static Vector2 _lastMousePos;

    public static FrameworkElement? Root
    {
        get => _root;
        set => _root = value;
    }

    public static FrameworkElement? HoveredElement => _hoveredElement;
    public static FrameworkElement? FocusedElement => _focusedElement;

    public static void Initialize(IInputContext input, FrameworkElement? root = null)
    {
        _root = root;

        foreach (var mouse in input.Mice)
        {
            mouse.MouseMove += (m, pos) => OnMouseMove(new Vector2(pos.X, pos.Y));
            mouse.MouseDown += (m, btn) => OnMouseDown(btn);
            mouse.MouseUp += (m, btn) => OnMouseUp(btn);
            mouse.Scroll += (m, scroll) => OnMouseScroll(new Vector2(scroll.X, scroll.Y));
        }

        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += (kb, key, code) => OnKeyDown(key);
            keyboard.KeyUp += (kb, key, code) => OnKeyUp(key);
            keyboard.KeyChar += (kb, c) => OnKeyChar(c);
        }
    }

    public static void SetFocus(FrameworkElement? element)
    {
        if (_focusedElement == element) return;

        var oldFocus = _focusedElement;
        _focusedElement = element;

        if (oldFocus is Control oldControl)
        {
            oldControl.IsFocused = false;
        }

        if (_focusedElement is Control newControl)
        {
            newControl.IsFocused = true;
        }
    }

    public static FrameworkElement? HitTest(Vector2 screenPoint)
    {
        if (_root == null) return null;
        return HitTestInternal(_root, screenPoint, Vector2.Zero);
    }

    private static FrameworkElement? HitTestInternal(Visual visual, Vector2 screenPoint, Vector2 parentOffset)
    {
        if (visual is not FrameworkElement fe || !fe.IsHitTestVisible || !fe.IsEnabled)
            return null;

        Vector2 localOffset = parentOffset + visual.Offset;
        Rect bounds = new Rect(localOffset, visual.Size);

        if (!bounds.Contains(screenPoint))
            return null;

        // Traverse children in reverse order (topmost first)
        if (visual is ContainerVisual container)
        {
            for (int i = container.Children.Count - 1; i >= 0; i--)
            {
                var child = container.Children[i];
                var hit = HitTestInternal(child, screenPoint, localOffset);
                if (hit != null)
                    return hit;
            }
        }

        return fe;
    }

    public static Vector2 GetLocalPosition(Visual? visual, Vector2 screenPoint)
    {
        if (visual == null) return screenPoint;
        Vector2 globalOffset = Vector2.Zero;
        Visual? current = visual;
        while (current != null)
        {
            globalOffset += current.Offset;
            current = current.Parent;
        }
        return screenPoint - globalOffset;
    }

    private static List<FrameworkElement> GetVisualPath(FrameworkElement? element)
    {
        var path = new List<FrameworkElement>();
        var current = element;
        while (current != null)
        {
            path.Insert(0, current);
            current = current.Parent as FrameworkElement;
        }
        return path;
    }

    private static void OnMouseMove(Vector2 screenPos)
    {
        _lastMousePos = screenPos;
        var hit = HitTest(screenPos);

        if (hit != _hoveredElement)
        {
            var oldPath = GetVisualPath(_hoveredElement);
            var newPath = GetVisualPath(hit);

            int commonIndex = -1;
            for (int i = 0; i < Math.Min(oldPath.Count, newPath.Count); i++)
            {
                if (oldPath[i] == newPath[i])
                {
                    commonIndex = i;
                }
                else
                {
                    break;
                }
            }

            // PointerExited events: leaf to common ancestor exclusive
            for (int i = oldPath.Count - 1; i > commonIndex; i--)
            {
                oldPath[i].OnPointerExited(new PointerRoutedEventArgs
                {
                    Position = GetLocalPosition(oldPath[i], screenPos),
                    ScreenPosition = screenPos
                });
            }

            // PointerEntered events: common ancestor exclusive to leaf
            for (int i = commonIndex + 1; i < newPath.Count; i++)
            {
                newPath[i].OnPointerEntered(new PointerRoutedEventArgs
                {
                    Position = GetLocalPosition(newPath[i], screenPos),
                    ScreenPosition = screenPos
                });
            }

            _hoveredElement = hit;
        }

        if (_hoveredElement != null)
        {
            _hoveredElement.OnPointerMoved(new PointerRoutedEventArgs
            {
                Position = GetLocalPosition(_hoveredElement, screenPos),
                ScreenPosition = screenPos
            });
        }
    }

    private static void OnMouseDown(MouseButton button)
    {
        if (button != MouseButton.Left) return;

        var hit = HitTest(_lastMousePos);
        if (hit != null)
        {
            hit.OnPointerPressed(new PointerRoutedEventArgs
            {
                Position = GetLocalPosition(hit, _lastMousePos),
                ScreenPosition = _lastMousePos,
                IsLeftButtonPressed = true
            });
        }
        else
        {
            // Clicked outside any element: clear focus
            SetFocus(null);
        }
    }

    private static void OnMouseUp(MouseButton button)
    {
        if (button != MouseButton.Left) return;

        var hit = HitTest(_lastMousePos);
        if (hit != null)
        {
            hit.OnPointerReleased(new PointerRoutedEventArgs
            {
                Position = GetLocalPosition(hit, _lastMousePos),
                ScreenPosition = _lastMousePos,
                IsLeftButtonPressed = false
            });
        }
    }

    private static void OnMouseScroll(Vector2 scroll)
    {
        var hit = HitTest(_lastMousePos);
        if (hit != null)
        {
            hit.OnPointerWheelChanged(new PointerRoutedEventArgs
            {
                Position = GetLocalPosition(hit, _lastMousePos),
                ScreenPosition = _lastMousePos,
                WheelDelta = scroll.Y
            });
        }
    }

    private static void OnKeyDown(Key key)
    {
        if (_focusedElement != null)
        {
            _focusedElement.OnKeyDown(new KeyRoutedEventArgs { Key = key });
        }
    }

    private static void OnKeyUp(Key key)
    {
        if (_focusedElement != null)
        {
            _focusedElement.OnKeyUp(new KeyRoutedEventArgs { Key = key });
        }
    }

    private static void OnKeyChar(char c)
    {
        if (_focusedElement != null)
        {
            _focusedElement.OnCharacterReceived(new CharacterReceivedRoutedEventArgs { Character = c });
        }
    }
}
