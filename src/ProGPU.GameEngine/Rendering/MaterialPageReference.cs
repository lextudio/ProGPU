using System.Numerics;
using System.Runtime.InteropServices;

namespace ProGPU.GameEngine.Rendering;

/// <summary>64-byte source instance shared by all its material pages. App-owned
/// material parameters and source size retain their exact float32 representation.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct MaterialSourceInstance(Vector4 Bounds, Vector4 Color,
    Vector4 Parameters, Vector4 SourceSize);

/// <summary>48-byte page reference: normalized source/atlas rectangles followed by
/// four uint32 words. SourceIndex addresses a populated source instance; Resident is
/// exactly zero or one. Reserved words must be zero. One source may serve many pages.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct MaterialPageReference(Vector4 SourceRect, Vector4 AtlasRect,
    uint SourceIndex, uint Resident, uint Reserved0 = 0, uint Reserved1 = 0);
