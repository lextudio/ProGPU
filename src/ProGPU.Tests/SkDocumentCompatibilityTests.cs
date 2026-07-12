using SkiaSharp;
using Xunit;

namespace ProGPU.Tests;

public sealed class SkDocumentCompatibilityTests
{
    [Fact]
    public void PdfMetadataDefaultsConstructorsAndEqualityMatchSkia()
    {
        var defaults = SKDocumentPdfMetadata.Default;
        Assert.Equal(72f, defaults.RasterDpi);
        Assert.Equal(101, defaults.EncodingQuality);
        Assert.False(defaults.PdfA);

        var dpi = new SKDocumentPdfMetadata(144f);
        Assert.Equal(144f, dpi.RasterDpi);
        Assert.Equal(101, dpi.EncodingQuality);
        var quality = new SKDocumentPdfMetadata(80);
        Assert.Equal(72f, quality.RasterDpi);
        Assert.Equal(80, quality.EncodingQuality);
        var combined = new SKDocumentPdfMetadata(96f, 75)
        {
            Title = "Title",
            Author = "Author",
            Creation = new DateTime(2026, 7, 12, 12, 30, 0, DateTimeKind.Local),
            PdfA = true,
        };
        var copy = combined;
        Assert.Equal(combined, copy);
        Assert.True(combined == copy);
        copy.Title = "Changed";
        Assert.NotEqual(combined, copy);
        Assert.True(combined != copy);

        var xps = new SKDocumentXpsOptions { Dpi = 120f, AllowNoPngs = true };
        Assert.Equal(xps, new SKDocumentXpsOptions { Dpi = 120f, AllowNoPngs = true });
        Assert.NotEqual(xps, new SKDocumentXpsOptions { Dpi = 120f, AllowNoPngs = false });
    }

    [Fact]
    public void EmptyAndAbortedDocumentsMatchNativeZeroOutputContract()
    {
        using var pdfOutput = new MemoryStream();
        using (var document = SKDocument.CreatePdf(pdfOutput))
        {
            document.Close();
        }

        Assert.Equal(0, pdfOutput.Length);
        Assert.True(pdfOutput.CanWrite);

        using var metadataOutput = new MemoryStream();
        using (var document = SKDocument.CreatePdf(
                   metadataOutput,
                   new SKDocumentPdfMetadata(72f) { Title = "metadata" }))
        {
            document.Close();
        }

        Assert.Equal(0, metadataOutput.Length);
        Assert.True(metadataOutput.CanWrite);

        using var abortedOutput = new MemoryStream();
        using (var document = SKDocument.CreatePdf(abortedOutput))
        {
            document.Abort();
        }

        Assert.Equal(0, abortedOutput.Length);
        Assert.True(abortedOutput.CanWrite);

        using var xpsOutput = new MemoryStream();
        using (var document = SKDocument.CreateXps(
                   xpsOutput,
                   new SKDocumentXpsOptions { Dpi = 96f, AllowNoPngs = true }))
        {
            document.Close();
        }

        Assert.Equal(0, xpsOutput.Length);
        Assert.True(xpsOutput.CanWrite);
    }

    [Fact]
    public void PathOverloadsOwnWritersAndInvalidInputsStayGuarded()
    {
        var pdfPath = Path.Combine(Path.GetTempPath(), $"progpu-{Guid.NewGuid():N}.pdf");
        var xpsPath = Path.Combine(Path.GetTempPath(), $"progpu-{Guid.NewGuid():N}.xps");
        try
        {
            using (var document = SKDocument.CreatePdf(pdfPath, SKDocumentPdfMetadata.Default))
            {
                document.Close();
            }

            using (var document = SKDocument.CreateXps(xpsPath, 96f))
            {
                document.Close();
            }

            Assert.True(File.Exists(pdfPath));
            Assert.True(File.Exists(xpsPath));
            Assert.Equal(0, new FileInfo(pdfPath).Length);
            Assert.Equal(0, new FileInfo(xpsPath).Length);
            Assert.Throws<ArgumentNullException>(() => SKDocument.CreatePdf((string)null!));
            Assert.Throws<ArgumentNullException>(() => SKDocument.CreatePdf((Stream)null!));
            Assert.Throws<ArgumentNullException>(() => SKDocument.CreateXps((SKWStream)null!));
        }
        finally
        {
            File.Delete(pdfPath);
            File.Delete(xpsPath);
        }
    }
}
