using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ToolingExtractor.Infrastructure.Pdf;

public class DigitalPdfExtractor : IPdfTextExtractor
{
    public Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (PdfClassifierWouldBeAmended(filePath))
            throw new InvalidOperationException("Amended PDFs must not use digital extraction.");

        using var doc = PdfDocument.Open(filePath);
        var lines = new List<string>();

        foreach (var page in doc.GetPages())
        {
            var words = page.GetWords().ToList();
            var rowGroups = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 3.0) * 3.0)
                .OrderByDescending(g => g.Key);

            foreach (var row in rowGroups)
            {
                var sorted = row.OrderBy(w => w.BoundingBox.Left).ToList();
                var columns = ClusterColumns(sorted, 15);
                lines.Add(string.Join("\t", columns.Select(c => string.Join(" ", c.Select(w => w.Text)))));
            }
        }

        return Task.FromResult(ApplyMultilineHeaderMerge(string.Join("\n", lines)));
    }

    private static bool PdfClassifierWouldBeAmended(string filePath) => false;

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
