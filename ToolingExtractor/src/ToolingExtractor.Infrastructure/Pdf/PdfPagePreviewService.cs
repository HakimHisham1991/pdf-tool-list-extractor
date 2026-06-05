using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage;
using SkiaSharp;
using ToolingExtractor.Core.Configuration;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Rendering.Skia;

namespace ToolingExtractor.Infrastructure.Pdf;

/// <summary>
/// Renders PDF pages for UI preview. Uses PDFium (via PDFtoImage) for full vector + text + image output;
/// falls back to PdfPig/Skia and embedded-image composite only when PDFium fails.
/// </summary>
public class PdfPagePreviewService
{
    private static readonly object PdfiumGate = new();

    private readonly ToolingExtractorOptions _options;
    private readonly ILogger<PdfPagePreviewService> _logger;

    public PdfPagePreviewService(IOptions<ToolingExtractorOptions> options, ILogger<PdfPagePreviewService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public int GetPageCount(string filePath)
    {
        if (!File.Exists(filePath))
            return 0;

        if (OperatingSystem.IsWindows())
        {
            var pdfiumCount = TryGetPageCountPdfium(filePath);
            if (pdfiumCount > 0)
                return pdfiumCount;
        }

        try
        {
            using var stream = OpenSharedRead(filePath);
            using var doc = PdfDocument.Open(stream);
            return doc.NumberOfPages;
        }
        catch
        {
            return 0;
        }
    }

    public byte[]? RenderPageJpeg(string filePath, int pageNumber, int maxEdgePixels = 1100)
    {
        if (!File.Exists(filePath))
            return null;

        var maxPixels = Math.Min(
            maxEdgePixels,
            _options.MaxPageRenderPixels > 0 ? _options.MaxPageRenderPixels : maxEdgePixels);

        if (OperatingSystem.IsWindows())
        {
            lock (PdfiumGate)
            {
                var pdfium = TryRenderPdfium(filePath, pageNumber, maxPixels);
                if (pdfium != null)
                    return pdfium;
            }
        }

        try
        {
            using var stream = OpenSharedRead(filePath);
            using var doc = PdfDocument.Open(stream);
            if (pageNumber < 1 || pageNumber > doc.NumberOfPages)
                return null;

            var jpeg = TryRenderSkiaPage(doc, pageNumber, maxPixels);
            if (jpeg != null)
                return jpeg;

            jpeg = TryCompositeEmbeddedImages(doc, pageNumber, maxPixels);
            if (jpeg != null)
                return jpeg;

            return TryRenderLargestPlacedImage(doc, pageNumber, maxPixels);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Preview render failed for {File} page {Page}", filePath, pageNumber);
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private int TryGetPageCountPdfium(string filePath)
    {
        try
        {
            using var stream = OpenSharedRead(filePath);
            return Conversion.GetPageCount(stream, leaveOpen: false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PDFium page count failed for {File}", filePath);
            return 0;
        }
    }

    [SupportedOSPlatform("windows")]
    private byte[]? TryRenderPdfium(string filePath, int pageNumber, int maxEdgePixels)
    {
        try
        {
            using var stream = OpenSharedRead(filePath);
            var pageIndex = pageNumber - 1;
            if (pageIndex < 0)
                return null;

            var dpi = ComputePreviewDpi(filePath, pageIndex, maxEdgePixels);
            var options = new RenderOptions(
                Dpi: dpi,
                Width: null,
                Height: maxEdgePixels,
                WithAnnotations: true,
                WithFormFill: true,
                WithAspectRatio: true,
                Rotation: PdfRotation.Rotate0,
                AntiAliasing: PdfAntiAliasing.All,
                BackgroundColor: SKColors.White,
                Bounds: null,
                UseTiling: true,
                DpiRelativeToBounds: false,
                Grayscale: false);

            using var bitmap = Conversion.ToImage(stream, pageIndex, leaveOpen: false, password: null, options);
            if (bitmap == null)
                return null;

            return EncodeJpeg(ScaleToMaxEdge(bitmap, maxEdgePixels));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PDFium preview failed for {File} page {Page}", filePath, pageNumber);
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private int ComputePreviewDpi(string filePath, int pageIndex, int maxEdgePixels)
    {
        try
        {
            using var stream = OpenSharedRead(filePath);
            var size = Conversion.GetPageSize(stream, pageIndex, leaveOpen: false);
            var longestInches = Math.Max(size.Width, size.Height) / 72.0;
            if (longestInches > 0)
            {
                var dpi = (int)Math.Round(maxEdgePixels / longestInches);
                return Math.Clamp(dpi, 72, Math.Max(72, _options.PdfRenderDpi));
            }
        }
        catch { /* use default */ }

        return Math.Clamp(150, 72, Math.Max(72, _options.PdfRenderDpi));
    }

    private static FileStream OpenSharedRead(string filePath) =>
        File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

    private byte[]? TryRenderSkiaPage(PdfDocument doc, int pageNumber, int maxEdgePixels)
    {
        try
        {
            doc.AddSkiaPageFactory();
            var page = doc.GetPage(pageNumber);
            var dpi = ComputeRenderDpi(page, maxEdgePixels);
            using var bitmap = doc.GetPageAsSKBitmap(pageNumber, dpi, SKColors.White);
            return EncodeJpeg(ScaleToMaxEdge(bitmap, maxEdgePixels));
        }
        catch (Exception ex) when (IsBitmapAllocationFailure(ex))
        {
            return null;
        }
    }

    private byte[]? TryCompositeEmbeddedImages(PdfDocument doc, int pageNumber, int maxEdgePixels)
    {
        var page = doc.GetPage(pageNumber);
        var images = page.GetImages().ToList();
        if (images.Count == 0)
            return null;

        var pageW = page.Width;
        var pageH = page.Height;
        if (pageW <= 0 || pageH <= 0)
            return null;

        var scale = maxEdgePixels / (float)Math.Max(pageW, pageH);
        var bmpW = Math.Max(1, (int)Math.Round(pageW * scale));
        var bmpH = Math.Max(1, (int)Math.Round(pageH * scale));
        var pageArea = pageW * pageH;

        using var surface = new SKBitmap(bmpW, bmpH);
        using var canvas = new SKCanvas(surface);
        canvas.Clear(SKColors.White);

        var drawn = 0;
        foreach (var pdfImage in images.OrderByDescending(img => PlacementArea(img.BoundingBox)))
        {
            var bounds = pdfImage.BoundingBox;
            if (PlacementArea(bounds) < pageArea * 0.005)
                continue;

            using var decoded = DecodePdfImage(pdfImage);
            if (decoded == null)
                continue;

            var dest = MapBoundsToDest(bounds, pageH, scale);
            if (dest.Width < 1 || dest.Height < 1)
                continue;

            canvas.DrawBitmap(decoded, dest);
            drawn++;
        }

        return drawn > 0 ? EncodeJpeg(surface.Copy()) : null;
    }

    private byte[]? TryRenderLargestPlacedImage(PdfDocument doc, int pageNumber, int maxEdgePixels)
    {
        var page = doc.GetPage(pageNumber);
        var pageArea = page.Width * page.Height;
        IPdfImage? best = null;
        var bestPlacement = 0.0;

        foreach (var image in page.GetImages())
        {
            var placement = PlacementArea(image.BoundingBox);
            if (placement > bestPlacement)
            {
                bestPlacement = placement;
                best = image;
            }
        }

        if (best == null || bestPlacement < pageArea * 0.05)
            return null;

        using var decoded = DecodePdfImage(best);
        if (decoded == null)
            return null;

        return EncodeJpeg(ScaleToMaxEdge(decoded, maxEdgePixels));
    }

    private static double PlacementArea(PdfRectangle bounds) =>
        Math.Max(0, bounds.Width) * Math.Max(0, bounds.Height);

    private static SKRect MapBoundsToDest(PdfRectangle bounds, double pageHeightPoints, float scale)
    {
        var left = (float)(bounds.Left * scale);
        var width = (float)(bounds.Width * scale);
        var height = (float)(bounds.Height * scale);
        var top = (float)((pageHeightPoints - bounds.Top) * scale);
        return SKRect.Create(left, top, width, height);
    }

    private static SKBitmap? DecodePdfImage(IPdfImage image)
    {
        byte[]? bytes = null;
        if (image.TryGetPng(out var png))
            bytes = png;
        else if (image.TryGetBytesAsMemory(out var decoded))
            bytes = decoded.ToArray();
        else if (image.RawBytes.Length > 0)
            bytes = image.RawBytes.ToArray();

        if (bytes == null || bytes.Length == 0)
            return null;

        try
        {
            using var data = SKData.CreateCopy(bytes);
            using var skImage = SKImage.FromEncodedData(data);
            return skImage == null ? null : SKBitmap.FromImage(skImage);
        }
        catch
        {
            return null;
        }
    }

    private static float ComputeRenderDpi(Page page, int maxEdgePixels)
    {
        var widthInches = page.Width / 72.0;
        var heightInches = page.Height / 72.0;
        if (widthInches <= 0 || heightInches <= 0)
            return 150f;

        var dpiW = maxEdgePixels / widthInches;
        var dpiH = maxEdgePixels / heightInches;
        return (float)Math.Clamp(Math.Min(dpiW, dpiH), 36, 150);
    }

    private static SKBitmap ScaleToMaxEdge(SKBitmap source, int maxEdgePixels)
    {
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= maxEdgePixels)
            return source.Copy();

        var scale = maxEdgePixels / (float)longest;
        var w = Math.Max(1, (int)Math.Round(source.Width * scale));
        var h = Math.Max(1, (int)Math.Round(source.Height * scale));
        var dest = new SKBitmap(w, h);
        using var canvas = new SKCanvas(dest);
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(source, SKRect.Create(0, 0, w, h));
        return dest;
    }

    private static byte[]? EncodeJpeg(SKBitmap bitmap)
    {
        try
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            return data?.ToArray();
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    private static bool IsBitmapAllocationFailure(Exception ex) =>
        ex.Message.Contains("allocate pixels", StringComparison.OrdinalIgnoreCase) ||
        (ex.InnerException != null && IsBitmapAllocationFailure(ex.InnerException));
}
