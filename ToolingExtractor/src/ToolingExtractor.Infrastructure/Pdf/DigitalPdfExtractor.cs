using System.Text.RegularExpressions;
using ToolingExtractor.Core;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ToolingExtractor.Infrastructure.Pdf;

public class DigitalPdfExtractor : IPdfTextExtractor
{
    private readonly IExtractionVisualizerNotifier? _visualizer;
    private static readonly Regex ToolRowRegex = new(
        @"^\s*(?:T\d{2,3}|\d{2})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DigitalPdfExtractor(IExtractionVisualizerNotifier? visualizer = null)
    {
        _visualizer = visualizer;
    }

    public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        using var doc = PdfDocument.Open(filePath);
        var lines = new List<string>();
        var jobId = ExtractionVisualizerScope.JobId;
        var pageCount = doc.NumberOfPages;
        var pageIndex = 0;

        foreach (var page in doc.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (jobId.HasValue)
            {
                _visualizer?.SetStage(
                    jobId.Value, "digital", $"Reading digital text — page {pageIndex + 1}/{pageCount}",
                    pageIndex, pageCount, (int)page.Width, (int)page.Height);
            }

            var words = page.GetWords().ToList();
            var rowGroups = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 3.0) * 3.0)
                .OrderByDescending(g => g.Key)
                .ToList();

            var pageBoxes = new List<HighlightBox>();
            VisualizerHighlightHelper.AddIgnoredBands(pageBoxes);

            var tableBounds = new List<(double L, double B, double R, double T)>();
            foreach (var row in rowGroups)
            {
                var sorted = row.OrderBy(w => w.BoundingBox.Left).ToList();
                var lineText = string.Join(" ", sorted.Select(w => w.Text));
                var isToolRow = ToolRowRegex.IsMatch(lineText) ||
                                lineText.Contains("Tool No", StringComparison.OrdinalIgnoreCase);

                if (isToolRow)
                {
                    foreach (var w in sorted)
                    {
                        tableBounds.Add((
                            w.BoundingBox.Left, w.BoundingBox.Bottom,
                            w.BoundingBox.Right, w.BoundingBox.Top));
                    }
                }

                if (pageBoxes.Count(b => b.Type == "ocr") < 400)
                {
                    foreach (var w in sorted)
                    {
                        pageBoxes.Add(VisualizerHighlightHelper.HighlightFromPdfRect(
                            w.BoundingBox.Left, w.BoundingBox.Bottom, w.BoundingBox.Right, w.BoundingBox.Top,
                            page.Width, page.Height, w.Text, "ocr", 1f));
                    }
                }

                var columns = ClusterColumns(sorted, 15);
                lines.Add(string.Join("\t", columns.Select(c => string.Join(" ", c.Select(w => w.Text)))));
            }

            if (tableBounds.Count > 0)
            {
                var minL = tableBounds.Min(b => b.L);
                var minB = tableBounds.Min(b => b.B);
                var maxR = tableBounds.Max(b => b.R);
                var maxT = tableBounds.Max(b => b.T);
                pageBoxes.Add(VisualizerHighlightHelper.HighlightFromPdfRect(
                    minL - 4, minB - 4, maxR + 4, maxT + 4,
                    page.Width, page.Height, "tool table", "table", 1f));
            }

            if (jobId.HasValue && pageBoxes.Count > 0)
            {
                _visualizer?.AddPageHighlights(jobId.Value, pageIndex + 1, pageBoxes);
                _visualizer?.SetStage(
                    jobId.Value, "digital", $"Digital text — {pageBoxes.Count} highlight region(s) on page {pageIndex + 1}",
                    pageIndex, pageCount, (int)page.Width, (int)page.Height, null, 0);
            }

            pageIndex++;
        }

        return Task.FromResult(ApplyMultilineHeaderMerge(string.Join("\n", lines)));
    }

    private static List<List<Word>> ClusterColumns(List<Word> words, double threshold)
    {
        var columns = new List<List<Word>>();
        foreach (var word in words)
        {
            var col = columns.FirstOrDefault(c =>
                Math.Abs(c[0].BoundingBox.Left - word.BoundingBox.Left) < threshold);
            if (col == null)
                columns.Add(new List<Word> { word });
            else
                col.Add(word);
        }
        return columns;
    }

    private static string ApplyMultilineHeaderMerge(string text)
    {
        var rawLines = text.Split('\n');
        var merged = new List<string>();
        for (var i = 0; i < rawLines.Length; i++)
        {
            var line = rawLines[i];
            if (i > 0 && !line.Contains(':') && merged.Count > 0 && merged[^1].Contains(':'))
                merged[^1] = merged[^1] + " " + line.Trim();
            else
                merged.Add(line);
        }
        return string.Join("\n", merged);
    }
}
