using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolingExtractor.Core.Configuration;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace ToolingExtractor.Infrastructure.Pdf;

public class AmendmentDetector : IAmendmentDetector
{
    private readonly double _coordProximityPt;
    private readonly double _annotOverlapPt;
    private readonly bool _enabled;
    private readonly ILogger<AmendmentDetector> _logger;

    public AmendmentDetector(IOptions<ToolingExtractorOptions> options, ILogger<AmendmentDetector> logger)
    {
        var ad = options.Value.AmendmentDetection;
        _enabled = ad.Enabled;
        _coordProximityPt = ad.CoordProximityPoints;
        _annotOverlapPt = ad.AnnotationOverlapPoints;
        _logger = logger;
    }

    public AmendmentReport Inspect(string filePath)
    {
        var report = new AmendmentReport { FilePath = filePath };
        if (!_enabled)
        {
            report.Summary = "Amendment detection disabled.";
            return report;
        }

        try
        {
            using var doc = PdfDocument.Open(filePath);
            foreach (var page in doc.GetPages())
            {
                var pageEvidence = new List<AmendmentEvidence>();
                var words = page.GetWords().ToList();

                TryDetectDuplicateWordCoords(page, words, pageEvidence);
                TryDetectAnnotationOverlay(page, words, pageEvidence);

                if (pageEvidence.Count > 0)
                {
                    report.Evidence.AddRange(pageEvidence);
                    report.AffectedPageCount++;
                }
            }

            report.IsAmended = report.Evidence.Count > 0;
            report.Summary = report.IsAmended
                ? $"Amendment detected: {report.Evidence.Count} evidence item(s) across {report.AffectedPageCount} page(s). Types: {string.Join(", ", report.Evidence.Select(e => e.EvidenceType).Distinct())}"
                : "No amendment evidence found.";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Amendment inspection failed for {File}", filePath);
            report.IsAmended = false;
            report.Summary = $"Inspection failed: {ex.Message}";
        }

        return report;
    }

    private void TryDetectDuplicateWordCoords(Page page, List<Word> words, List<AmendmentEvidence> pageEvidence)
    {
        var grouped = words.GroupBy(w => Math.Round(w.BoundingBox.Bottom / _coordProximityPt) * _coordProximityPt);
        foreach (var lineGroup in grouped)
        {
            var byX = lineGroup.GroupBy(w => Math.Round(w.BoundingBox.Left / _coordProximityPt) * _coordProximityPt);
            foreach (var xGroup in byX)
            {
                // Only flag when the same text appears more than once at the same position (true overlay),
                // not when different column values share a rounded grid cell (common on tooling tables).
                foreach (var dup in xGroup.GroupBy(w => w.Text.Trim(), StringComparer.OrdinalIgnoreCase)
                             .Where(g => g.Count() > 1))
                {
                    pageEvidence.Add(new AmendmentEvidence
                    {
                        PageNumber = page.Number,
                        EvidenceType = "DUPLICATE_COORD",
                        Detail = $"Page {page.Number}: duplicate '{dup.Key}' at ({xGroup.Key:F0},{lineGroup.Key:F0}) x{dup.Count()}"
                    });
                }
            }
        }
    }

    private void TryDetectAnnotationOverlay(Page page, List<Word> words, List<AmendmentEvidence> pageEvidence)
    {
        try
        {
            foreach (var annotation in page.GetAnnotations())
            {
                var typeName = annotation.Type.ToString();
                if (!typeName.Contains("FreeText", StringComparison.OrdinalIgnoreCase) &&
                    !typeName.Contains("Stamp", StringComparison.OrdinalIgnoreCase) &&
                    !typeName.Contains("Widget", StringComparison.OrdinalIgnoreCase) &&
                    !typeName.Contains("Ink", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var word in words)
                {
                    if (BoundingBoxesOverlap(annotation.Rectangle, word.BoundingBox, _annotOverlapPt))
                    {
                        pageEvidence.Add(new AmendmentEvidence
                        {
                            PageNumber = page.Number,
                            EvidenceType = "ANNOTATION_OVERLAY",
                            Detail = $"Page {page.Number}: {annotation.Type} overlaps text '{word.Text}'"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Annotation inspection skipped on page {Page}", page.Number);
        }
    }

    private static bool BoundingBoxesOverlap(PdfRectangle a, PdfRectangle b, double tolerance = 2.0) =>
        a.Left < b.Right + tolerance && a.Right > b.Left - tolerance &&
        a.Bottom < b.Top + tolerance && a.Top > b.Bottom - tolerance;
}
