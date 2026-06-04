using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolingExtractor.Core.Configuration;
using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;
using UglyToad.PdfPig;

namespace ToolingExtractor.Infrastructure.Pdf;

public class PdfClassifier : IPdfClassifier
{
    private readonly IAmendmentDetector _amendmentDetector;
    private readonly ToolingExtractorOptions _options;
    private readonly ILogger<PdfClassifier> _logger;

    public PdfClassifier(
        IAmendmentDetector amendmentDetector,
        IOptions<ToolingExtractorOptions> options,
        ILogger<PdfClassifier> logger)
    {
        _amendmentDetector = amendmentDetector;
        _options = options.Value;
        _logger = logger;
    }

    public PdfType Classify(string filePath, out AmendmentReport? amendmentReport)
    {
        amendmentReport = null;

        var report = _amendmentDetector.Inspect(filePath);
        amendmentReport = report;

        try
        {
            using var doc = PdfDocument.Open(filePath);
            var pages = doc.GetPages().Take(3).ToList();
            if (pages.Count == 0)
                return report.IsAmended ? PdfType.Amended : PdfType.Scanned;

            var avgWords = pages.Average(p => p.GetWords().Count());

            if (report.IsAmended)
            {
                _logger.LogWarning("AMENDED PDF: {FilePath} — {Summary}", filePath, report.Summary);
                WriteAmendmentLog(filePath, report);
                // Rich embedded text (typical landscape WI/tool lists) — extract digitally, keep amendment flags.
                if (avgWords > 20)
                    return PdfType.Mixed;
                return PdfType.Amended;
            }

            if (avgWords > 20) return PdfType.Digital;
            if (avgWords >= 5) return PdfType.Mixed;
            return PdfType.Scanned;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF classification failed for {File}, defaulting to Scanned", filePath);
            return report.IsAmended ? PdfType.Amended : PdfType.Scanned;
        }
    }

    private void WriteAmendmentLog(string filePath, AmendmentReport report)
    {
        try
        {
            Directory.CreateDirectory(_options.AmendedPdfLogPath);
            var name = Path.GetFileNameWithoutExtension(filePath) + ".amendment.json";
            var path = Path.Combine(_options.AmendedPdfLogPath, name);
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write amendment log for {File}", filePath);
        }
    }
}
