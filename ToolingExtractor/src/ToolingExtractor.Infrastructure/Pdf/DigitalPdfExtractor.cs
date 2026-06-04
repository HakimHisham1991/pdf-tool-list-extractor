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
    private static readonly Regex ToolRowRegex = new(@"^\s*T\d{2}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

            var highlights = new List<VisualizerHighlight>();
            foreach (var row in rowGroups)
            {
                var sorted = row.OrderBy(w => w.BoundingBox.Left).ToList();
                var lineText = string.Join(" ", sorted.Select(w => w.Text));
                var isToolRow = ToolRowRegex.IsMatch(lineText) ||
                                lineText.Contains("Tool No", StringComparison.OrdinalIgnoreCase);

                if (isToolRow && highlights.Count < 120)
                {
                    foreach (var w in sorted)
                    {
                        highlights.Add(VisualizerHighlightHelper.FromPdfWord(
                            w.BoundingBox.Left, w.BoundingBox.Bottom, w.BoundingBox.Right, w.BoundingBox.Top,
                            page.Width, page.Height, w.Text, "digital"));
                    }
                }

                var columns = ClusterColumns(sorted, 15);
                lines.Add(string.Join("\t", columns.Select(c => string.Join(" ", c.Select(w => w.Text)))));
            }

            if (jobId.HasValue && highlights.Count > 0)
            {
                _visualizer?.SetStage(
                    jobId.Value, "digital", $"Highlighting table cells — page {pageIndex + 1}",
                    pageIndex, pageCount, (int)page.Width, (int)page.Height, highlights, 0);
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
