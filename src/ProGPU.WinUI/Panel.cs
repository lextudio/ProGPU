using System;
using ProGPU.Scene;

namespace ProGPU.WinUI;

public class Panel : FrameworkElement
{
    public new void AddChild(Visual child)
    {
        base.AddChild(child);
    }

    public new void RemoveChild(Visual child)
    {
        base.RemoveChild(child);
    }

    public new void ClearChildren()
    {
        base.ClearChildren();
    }
}
