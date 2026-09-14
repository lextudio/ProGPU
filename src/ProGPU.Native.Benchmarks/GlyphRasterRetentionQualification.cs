using System.Numerics;
using ProGPU.Backend;
using ProGPU.Backend.Native;

// Run with each configured compute/raster/SIMD/scalar implementation. The
// uncached render is an independent resource-rebuild oracle, not a UV snapshot.
internal static class GlyphRasterRetentionQualification
{
    internal static void Run(NativeCompositor renderer, GpuTexture target)
    {
        NativePathSegment[] segments =
        [
            new(NativePathSegmentKind.Line, new(0, 0), new(12, 0)),
            new(NativePathSegmentKind.Quadratic, new(12, 0), new(18, 7), new(12, 14)),
            new(NativePathSegmentKind.Line, new(12, 14), new(0, 14)),
            new(NativePathSegmentKind.Line, new(0, 14), new(0, 0))
        ];
        NativeGlyphOutline[] outlines = [new(0, 4, new(0, 0), new(18, 14), 1)];
        NativePositionedGlyph[] glyphs =
            [new(0, new(30, 50), Vector2.UnitX, Vector2.UnitY, new(1, 0, 0, 1))];
        uint revision = 20;

        void Check(string label, NativeGlyphOutline[] nextOutlines,
            NativePathSegment[] nextSegments, NativePositionedGlyph[] nextGlyphs,
            float dpi, bool reuse)
        {
            var seed = renderer.RenderGlyphs(target, 1, outlines, segments, glyphs,
                Vector4.Zero, contentRevision: revision++);
            var stable = renderer.RenderGlyphs(target, 1, outlines, segments, glyphs,
                Vector4.Zero, contentRevision: revision - 1);
            if (stable.RasterizedGlyphCount != 0 || stable.OutlineUploadBytes != 0 ||
                stable.CoverageStagingBytes != 0 || stable.InstanceUploadBytes != 0)
            {
                throw new InvalidOperationException("Stable glyph replay uploaded retained resources.");
            }
            var changed = renderer.RenderGlyphs(target, dpi, nextOutlines,
                nextSegments, nextGlyphs, Vector4.Zero, contentRevision: revision++);
            if (reuse
                ? changed.RasterizedGlyphCount != 0 || changed.OutlineUploadBytes != 0 ||
                  changed.CoverageStagingBytes != 0 ||
                  changed.AtlasGeneration != seed.AtlasGeneration ||
                  changed.InstanceUploadBytes == 0
                : changed.RasterizedGlyphCount != nextOutlines.Length)
            {
                throw new InvalidOperationException($"Glyph raster retention failed: {label}: {changed}.");
            }
            byte[] retained = target.ReadPixels();
            var fresh = renderer.RenderGlyphs(target, dpi, nextOutlines,
                nextSegments, nextGlyphs, Vector4.Zero, contentRevision: 0);
            byte[] reference = target.ReadPixels();
            if (fresh.RasterizedGlyphCount != nextOutlines.Length ||
                !retained.AsSpan().SequenceEqual(reference))
            {
                throw new InvalidOperationException($"Glyph raster pixels differ from uncached rendering: {label}.");
            }
            if (nextGlyphs.Length != 0 && !reference.Any(static value => value != 0))
            {
                throw new InvalidOperationException($"Glyph raster qualification drew no ink: {label}.");
            }
        }

        Check("revision", outlines, segments, glyphs, 1, reuse: true);
        NativePositionedGlyph[] moved =
            [new(0, new(47, 58), Vector2.UnitX, Vector2.UnitY, new(0, 1, 0, 0.75f))];
        Check("placement and paint", outlines, segments, moved, 1, reuse: true);
        Check("instance count", outlines, segments, [glyphs[0], moved[0]], 1, reuse: true);
        Check("DPI", outlines, segments, glyphs, 2, reuse: false);
        Check("phase", [new(0, 4, new(0, 0), new(18, 14), 1, 0.25f)],
            segments, glyphs, 1, reuse: false);
        Check("scale", [new(0, 4, new(0, 0), new(18, 14), 1.5f)],
            segments, glyphs, 1, reuse: false);
        Check("bounds", [new(0, 4, new(-1, -1), new(19, 15), 1)],
            segments, glyphs, 1, reuse: false);
        NativePathSegment[] changedSegments = (NativePathSegment[])segments.Clone();
        changedSegments[1] = new(NativePathSegmentKind.Quadratic,
            new(12, 0), new(10, 7), new(12, 14));
        Check("segment bytes", outlines, changedSegments, glyphs, 1, reuse: false);
        Check("empty", [], [], [], 1, reuse: false);
        Check("after empty", outlines, segments, moved, 1, reuse: true);
        bool rejected = false;
        try
        {
            renderer.RenderGlyphs(target, 1,
                [new(0, 4, new(0, 0), new(18, 14), float.NaN)],
                segments, glyphs, Vector4.Zero, contentRevision: revision++);
        }
        catch (NativeRendererException error) when (error.Status == NativeRendererStatus.InvalidArgument)
        {
            rejected = true;
        }
        if (!rejected)
        {
            throw new InvalidOperationException("Malformed raster input was accepted by the retained cache.");
        }
        Check("after rejected input", outlines, segments, moved, 1, reuse: true);
        Console.Error.WriteLine("Glyph raster retention: 11 exact uncached pixel comparisons, stable replay and rejected-input recovery passed.");
    }
}
