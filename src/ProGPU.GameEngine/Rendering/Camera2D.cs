using System.Numerics;

namespace ProGPU.GameEngine.Rendering;

/// <summary>
/// Immutable, axis-aligned orthographic camera. Position is in world units and
/// scale is logical pixels per world unit; framebuffer DPI is a separate host
/// concern. Per-axis camera factors express parallax without mutating world data.
/// Construct explicitly (default has no valid scale). All operations are O(1).
/// </summary>
public readonly record struct Camera2D
{
    public Vector2 Position { get; }
    public float Scale { get; }

    public Camera2D(Vector2 position, float scale)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) throw new ArgumentOutOfRangeException(nameof(position));
        if (!float.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        Position = position; Scale = scale;
    }

    public Vector2 Project(Vector2 position, Vector2 cameraFactor) =>
        new((position.X - Position.X * cameraFactor.X) * Scale, (position.Y - Position.Y * cameraFactor.Y) * Scale);

    public Vector2 Unproject(Vector2 position, Vector2 cameraFactor) =>
        new(position.X / Scale + Position.X * cameraFactor.X, position.Y / Scale + Position.Y * cameraFactor.Y);

    public Vector4 ProjectBounds(in SpritePlacement placement)
    {
        if (!placement.IsWorldSpace) return placement.Bounds;
        var bounds = placement.Bounds;
        return new((bounds.X - Position.X * placement.CameraFactor.X) * Scale,
            (bounds.Y - Position.Y * placement.CameraFactor.Y) * Scale, bounds.Z * Scale, bounds.W * Scale);
    }
}

/// <summary>
/// An original source rectangle in world units or logical screen pixels. Dynamic
/// is a packing hint only: renderers must still compare static record changes.
/// CameraFactor is used only for world placement; ordinary world objects use (1,1).
/// </summary>
public readonly record struct SpritePlacement(Vector4 Bounds, Vector2 CameraFactor, bool IsWorldSpace, bool Dynamic)
{
    public static SpritePlacement World(Vector4 bounds, Vector2 cameraFactor, bool dynamic = false) => new(bounds, cameraFactor, true, dynamic);
    public static SpritePlacement Screen(Vector4 bounds, bool dynamic = false) => new(bounds, Vector2.Zero, false, dynamic);
}
