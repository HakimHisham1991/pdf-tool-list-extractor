using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ToolingExtractor.Core.Configuration;
using ToolingExtractor.Infrastructure.Pdf;
using Xunit;

namespace ToolingExtractor.Infrastructure.Tests;

public class PdfPagePreviewServiceTests
{
    [Fact]
    public void RenderPageJpeg_UploadedWiPdf_ReturnsNonEmptyJpeg()
    {
        var pdfPath = FindUploadedWiPdf();
        if (pdfPath == null)
            return;

        var svc = new PdfPagePreviewService(
            Options.Create(new ToolingExtractorOptions { PdfRenderDpi = 150, MaxPageRenderPixels = 1100 }),
            NullLogger<PdfPagePreviewService>.Instance);

        var jpeg = svc.RenderPageJpeg(pdfPath, 1);
        Assert.NotNull(jpeg);
        Assert.True(jpeg!.Length > 1000, $"Expected JPEG bytes, got {jpeg.Length}");
        Assert.Equal(0xFF, jpeg[0]);
        Assert.Equal(0xD8, jpeg[1]);
    }

    private static string? FindUploadedWiPdf()
    {
        var roots = new[]
        {
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ToolingExtractor.Web")
        };

        foreach (var root in roots)
        {
            try
            {
                var dir = Path.Combine(Path.GetFullPath(root), "data", "uploads");
                if (!Directory.Exists(dir)) continue;
                var hit = Directory
                    .EnumerateFiles(dir, "TYPE_1_F57551907200 OP10 REV01_WI.pdf", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (hit != null) return hit;
            }
            catch { /* ignore */ }
        }

        return null;
    }
}
