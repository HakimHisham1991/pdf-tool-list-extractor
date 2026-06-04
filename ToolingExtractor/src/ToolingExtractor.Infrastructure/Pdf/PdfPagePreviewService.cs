using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;
using ToolingExtractor.Core.Configuration;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;

namespace ToolingExtractor.Infrastructure.Pdf;

public class PdfPagePreviewService
{
    private readonly ToolingExtractorOptions _options;
    private readonly ILogger<PdfPagePreviewService> _logger;

    public PdfPagePreviewService(IOptions<ToolingExtractorOptions> options, ILogger<PdfPagePreviewService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public byte[]? RenderPageJpeg(string filePath, int pageNumber, int maxEdgePixels = 1100)
    {
        try
        {
            using var doc = PdfDocument.Open(filePath);
            doc.AddSkiaPageFactory();
            if (pageNumber < 1 || pageNumber > doc.NumberOfPages)
                return null;

            var page = doc.GetPage(pageNumber);
            var dpi = (float)_options.PdfRenderDpi;
            var maxPixels = Math.Min(maxEdgePixels, _options.MaxPageRenderPixels > 0 ? _options.MaxPageRenderPixels : maxEdgePixels);
            var widthInches = page.Width / 72.0;
            var heightInches = page.Height / 72.0;
            var longest = Math.Max(widthInches, heightInches);
            if (longest > 0)
                dpi = Math.Min(dpi, Math.Max(72f, (float)(maxPixels / longest)));

            using var bitmap = doc.GetPageAsSKBitmap(pageNumber, dpi, SKColors.White);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 82);
            return data?.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Preview render failed for {File} page {Page}", filePath, pageNumber);
            return null;
        }
    }
}
