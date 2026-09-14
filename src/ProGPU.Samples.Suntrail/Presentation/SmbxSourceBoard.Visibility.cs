using System.Numerics;
using ProGPU.GameEngine.Rendering;
using ProGPU.GameEngine.Simulation;
using ProGPU.Samples.Suntrail.Game.Import;

namespace ProGPU.Samples.Suntrail.Presentation;

public sealed partial class SmbxSourceBoard
{
    private StaticSpatialIndex2D? _spatial;
    private SmbxSourceDocument? _spatialDocument;
    private SmbxBoardArtwork? _spatialArtwork;
    private int[] _spatialIds = [];

    private void ClearSpatialGeneration()
    { _spatial = null; _spatialDocument = null; _spatialArtwork = null; }

    private void PrepareSpatialGeneration(SmbxGeometryEditor editor)
    {
        if (_spatial is not null && ReferenceEquals(_spatialDocument, editor.Document) && ReferenceEquals(_spatialArtwork, PreparedArtwork)) return;
        var records = new SpatialItem2D[editor.Geometry.Length];
        for (int i = 0; i < records.Length; i++)
        {
            var b = editor.Geometry[i].Bounds;
            double left = b.X, top = b.Y, right = b.X + Math.Max(1, b.Width), bottom = b.Y + Math.Max(1, b.Height);
            if (PreparedArtwork?[i] is not null)
            {
                var sprite = PreparedArtwork.Bounds(i, b);
                left = Math.Min(left, sprite.X); top = Math.Min(top, sprite.Y);
                right = Math.Max(right, sprite.X + sprite.Width); bottom = Math.Max(bottom, sprite.Y + sprite.Height);
            }
            double width = right - left, height = bottom - top;
            if (left + width < right) width = Math.BitIncrement(width);
            if (top + height < bottom) height = Math.BitIncrement(height);
            records[i] = new(i, new(left, top, width, height));
        }
        var next = new StaticSpatialIndex2D(records);
        if (_spatialIds.Length < records.Length)
            Array.Resize(ref _spatialIds, (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(16, records.Length)));
        _spatial = next; _spatialDocument = editor.Document; _spatialArtwork = PreparedArtwork;
    }

    private int QueryGeometry(SmbxGeometryEditor editor, double marginPixels)
    {
        double width = (Size.X + marginPixels * 2) / Zoom, height = (Size.Y + marginPixels * 2) / Zoom;
        return QueryGeometryBounds(editor, new(CameraX - width / 2, CameraY - height / 2, width, height));
    }

    private int QueryGeometryBounds(SmbxGeometryEditor editor, Bounds2D viewport)
    {
        PrepareSpatialGeneration(editor);
        int count = _spatial!.QueryOrdered(viewport, _spatialIds);
        if (editor.IsMoving && editor.Selected >= 0)
        {
            // Pointer previews do not rebuild the immutable source tree. Include
            // the dragged item explicitly even when it moved out of its old leaf.
            int position = _spatialIds.AsSpan(0, count).BinarySearch(editor.Selected);
            if (position < 0)
            {
                position = ~position;
                _spatialIds.AsSpan(position, count - position).CopyTo(_spatialIds.AsSpan(position + 1));
                _spatialIds[position] = editor.Selected; count++;
            }
        }
        return count;
    }
}
