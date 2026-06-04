using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;
using Sdcb.PaddleOCR;
using SkiaSharp;
using ToolingExtractor.Core.Configuration;
using ToolingExtractor.Core.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;

namespace ToolingExtractor.Infrastructure.Pdf;

public class ScannedPdfExtractor : IPdfTextExtractor
{
    private readonly PaddleOcrAll _ocr;
    private readonly ImagePreprocessor _preprocessor;
    private readonly ToolingExtractorOptions _options;
    private readonly ILogger<ScannedPdfExtractor> _logger;
    private readonly SemaphoreSlim _renderSemaphore = new(2);
    public float LastMinConfidence { get; private set; } = 1f;
    public int LastPageOrientation { get; private set; }

    public ScannedPdfExtractor(
        PaddleOcrAll ocr,
        ImagePreprocessor preprocessor,
        IOptions<ToolingExtractorOptions> options,
        ILogger<ScannedPdfExtractor> logger)
    {
        _ocr = ocr;
        _preprocessor = preprocessor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        LastMinConfidence = 1f;
        LastPageOrientation = 0;
        var pageTexts = new List<string>();

        await _renderSemaphore.WaitAsync(cancellationToken);
        try
        {
            using var doc = PdfDocument.Open(filePath);
            doc.AddSkiaPageFactory();

            for (var pageNumber = 1; pageNumber <= doc.NumberOfPages; pageNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SKBitmap? bitmap = null;
                SKBitmap? working = null;
                try
                {
                    var page = doc.GetPage(pageNumber);
                    var renderDpi = GetEffectiveRenderDpi(page);
                    if (renderDpi < _options.PdfRenderDpi)
                        _logger.LogDebug(
                            "Page {Page} of {File}: rendering at {Dpi} DPI (cap {Max}px)",
                            pageNumber, filePath, renderDpi, _options.MaxPageRenderPixels);

                    bitmap = doc.GetPageAsSKBitmap(pageNumber, renderDpi, SKColors.White);
                    var pdfRotation = GetPageRotationDegrees(page);
                    var (rotated, angle) = pdfRotation != 0
                        ? (Rotate(bitmap, pdfRotation), pdfRotation)
                        : await DetectAndApplyOrientationAsync(bitmap, cancellationToken);
                    LastPageOrientation = angle;
                    if (angle != 0)
                        _logger.LogInformation("Page {Page} of {File}: rotated {Angle}° for orientation correction.", pageNumber, filePath, angle);

                    working = rotated;
                    if (_preprocessor.ShouldPreprocess(working))
                    {
                        var processed = _preprocessor.Preprocess(working);
                        if (!ReferenceEquals(processed, working))
                        {
                            if (rotated != bitmap) rotated.Dispose();
                            working.Dispose();
                            working = processed;
                        }
                    }

                    using var mat = SkiaOpenCvHelper.ToMat(working);
                    var result = _ocr.Run(mat);
                    var filtered = result.Regions
                        .Where(r => r.Score >= _options.OcrConfidenceThreshold)
                        .ToList();

                    if (filtered.Count > 0)
                    {
                        var minConf = filtered.Min(r => r.Score);
                        LastMinConfidence = Math.Min(LastMinConfidence, minConf);
                        if (minConf < 0.75)
                            _logger.LogWarning("Low OCR confidence {Conf:F2} on page {Page} of {File}", minConf, pageNumber, filePath);
                    }

                    pageTexts.Add(OcrTableSorter.SortRegionsToTableText(filtered, working.Height));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OCR failed for page {Page} of {File}, skipping page", pageNumber, filePath);
                }
                finally
                {
                    bitmap?.Dispose();
                    if (working != null && working != bitmap)
                        working.Dispose();
                }
            }
        }
        finally
        {
            _renderSemaphore.Release();
        }

        return string.Join("\n", pageTexts);
    }

    private static int GetPageRotationDegrees(UglyToad.PdfPig.Content.Page page)
    {
        var raw = page.Rotation.Value;
        return raw switch
        {
            90 or 180 or 270 => raw,
            _ => 0
        };
    }

    private float GetEffectiveRenderDpi(UglyToad.PdfPig.Content.Page page)
    {
        var targetDpi = (float)_options.PdfRenderDpi;
        var maxPixels = _options.MaxPageRenderPixels;
        if (maxPixels <= 0)
            return targetDpi;

        var widthInches = page.Width / 72.0;
        var heightInches = page.Height / 72.0;
        var longestInches = Math.Max(widthInches, heightInches);
        if (longestInches <= 0)
            return targetDpi;

        var cappedDpi = (float)(maxPixels / longestInches);
        return Math.Min(targetDpi, Math.Max(72f, cappedDpi));
    }

    private async Task<(SKBitmap Bitmap, int Angle)> DetectAndApplyOrientationAsync(
        SKBitmap bitmap, CancellationToken ct)
    {
        var angles = new[] { 0, 90, 180, 270 };
        var bestAngle = 0;
        var bestCount = 0;

        foreach (var angle in angles)
        {
            using var rotated = angle == 0 ? bitmap.Copy() : Rotate(bitmap, angle);
            using var crop = CropCenter(rotated);
            var count = await CountOcrWordsAsync(crop, ct);
            if (count > bestCount)
            {
                bestCount = count;
                bestAngle = angle;
            }
        }

        return bestAngle == 0 ? (bitmap.Copy(), 0) : (Rotate(bitmap, bestAngle), bestAngle);
    }

    private Task<int> CountOcrWordsAsync(SKBitmap crop, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var mat = SkiaOpenCvHelper.ToMat(crop);
        var result = _ocr.Run(mat);
        return Task.FromResult(result.Regions.Count(r => r.Score >= _options.OcrConfidenceThreshold));
    }

    private static SKBitmap CropCenter(SKBitmap source)
    {
        var w = source.Width / 2;
        var h = source.Height / 2;
        var x = (source.Width - w) / 2;
        var y = (source.Height - h) / 2;
        var dest = new SKBitmap(w, h);
        using var canvas = new SKCanvas(dest);
        canvas.DrawBitmap(source, SKRect.Create(x, y, w, h), SKRect.Create(0, 0, w, h));
        return dest;
    }

    private static SKBitmap Rotate(SKBitmap source, int degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        var matrix = SKMatrix.CreateRotation((float)radians, source.Width / 2f, source.Height / 2f);
        var bounds = new SKRect(0, 0, source.Width, source.Height);
        matrix.MapRect(bounds);
        var dest = new SKBitmap((int)bounds.Width, (int)bounds.Height);
        using var canvas = new SKCanvas(dest);
        canvas.Clear(SKColors.White);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0);
        return dest;
    }
}
