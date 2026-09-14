using ProGPU.Backend.Native;

internal static class TextContinuationValidation
{
    internal static void Run(string fontPath)
    {
        using var context = new NativeTextShapingContext(File.ReadAllBytes(fontPath));
        int cases = 0;
        foreach (string text in new[] { "AVATAR office alpha beta gamma delta", "abc \u05d0\u05d1\u05d2 def ghi jkl mno", "a\U0001f642b\talpha beta gamma delta" })
        foreach (var direction in new[] { NativeTextDirection.LeftToRight, NativeTextDirection.RightToLeft })
        foreach (var alignment in new[] { NativeTextAlignment.Left, NativeTextAlignment.Justify })
        {
            var options = new NativeTextParagraphOptions(16f / 2048, 80, 20, Alignment: alignment);
            NativeTextParagraphStyle[] styles = [new(0, 4, 0, 16f / 2048), new(4, text.Length - 4, 0, 20f / 2048)];
            var original = NativeTextParagraphSnapshot.Create(context, text, direction, options,
                styles: styles, incrementalTab: 32, tabOrigin: 3);
            Require(original.Lines.Length > 1, "fixture must wrap");
            int start = original.Lines.Span[1].InputStart;
            foreach (float width in new[] { 40f, 120f })
            {
                var continued = NativeTextParagraphSnapshot.CreateContinued(context, text, direction,
                    options with { MaximumWidth = width }, start, styles: styles, incrementalTab: 32, tabOrigin: 3);
                Require(continued.Lines.Span[0].InputStart == start, "original continuation boundary");
                Require(continued.Lines.Span[^1].InputEnd == text.Length, "complete remaining input");
                int expected = original.Glyphs.Span.ToArray().Count(g => g.Cluster >= start);
                Require(continued.Glyphs.Length == expected, "no omitted or duplicated glyphs");
                for (int i = 0; i < continued.Glyphs.Length; i++)
                {
                    var glyph = continued.Glyphs.Span[i];
                    int source = -1;
                    for (int j = 0; j < original.Glyphs.Length; j++)
                        if (original.Glyphs.Span[j].GlyphIndex == glyph.GlyphIndex) { source = j; break; }
                    Require(source >= 0, "full-paragraph glyph identity");
                    var before = original.Glyphs.Span[source];
                    Require(glyph.GlyphId == before.GlyphId && glyph.FontIndex == before.FontIndex &&
                        glyph.Cluster == before.Cluster && continued.ClusterEnds.Span[i] == original.ClusterEnds.Span[source] &&
                        continued.BidiLevels.Span[i] == original.BidiLevels.Span[source], "shaping, cluster, face and bidi retained");
                }
                Require(continued.Boxes.Length > 0 && continued.Carets.Length > 0, "native interaction retained");
                // Existing collapse requires unchanged advances; justified-sign
                // semantics are a separate contract from continuation layout.
                if (alignment == NativeTextAlignment.Left)
                {
                    var collapsed = NativeTextParagraphSnapshot.CreateCollapsed(context, text, direction,
                        options with { MaximumWidth = width }, continued,
                        new(0, 15, 3, NativeTextTrimming.CharacterEllipsis), styles: styles, incrementalTab: 32, tabOrigin: 3);
                    Require(collapsed.Lines.Span[0].InputStart == start && collapsed.CollapsedRange is not null,
                        "continued collapse retains original boundary");
                }
                cases++;
            }
        }
        const string inline = "A\ufffcB\ufffcC";
        NativeTextParagraphStyle[] inlineStyles = [new(0, inline.Length, 0, 16f / 2048)];
        NativeTextStyleMetrics[] metrics = [new() { Ascent = 12, Descent = 4 }];
        NativeTextParagraphInlineObject[] objects = [new(1, 30, 35, 7), new(3, 20, 18, 3)];
        foreach (int start in new[] { 1, 2, 3, 4 })
        {
            var continued = NativeTextParagraphSnapshot.CreateContinued(context, inline, NativeTextDirection.LeftToRight,
                new(16f / 2048, 31, 20), start, styles: inlineStyles, measuredLines: true,
                styleMetrics: metrics, inlineObjects: objects);
            Require(continued.HasMeasuredLines && continued.Lines.Span[0].InputStart == start, "measured suffix origin");
            Require(continued.InlineObjects.Length == objects.Count(o => o.Position >= start), "remaining original objects only");
            foreach (var placement in continued.InlineObjects.Span)
                Require(placement.InputPosition >= start && placement.GlyphIndex >= 0 && placement.LineIndex >= 0,
                    "original inline source index and retained placement");
            cases++;
        }
        bool rejected = false;
        try
        {
            _ = NativeTextParagraphSnapshot.CreateContinued(context, "a\U0001f642b", NativeTextDirection.LeftToRight,
                new(16f / 2048, 30, 20), 2);
        }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "reject surrogate interior rather than rounding the boundary");
        Console.WriteLine($"Native continued text package contract passed: {cases} layouts and invalid-boundary rejection.");
    }

    private static void Require(bool valid, string contract)
    {
        if (!valid) throw new InvalidOperationException($"Native continued text: {contract}.");
    }
}
