using System;
using System.Numerics;
using ProGPU.Scene;

namespace ProGPU.Layout;

public interface ITemplatedControl
{
    bool HasTemplate { get; }
    Vector2 MeasureTemplate(Vector2 availableSize);
    void ArrangeTemplate(Rect arrangeRect);
}
