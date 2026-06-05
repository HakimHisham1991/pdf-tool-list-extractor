using System.Text.RegularExpressions;

namespace ToolingExtractor.Core.Models;

public static class ToolingRecordOrdering
{
    private static readonly Regex ToolNoKeyRegex = new(
        @"T(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Sort tooling rows in PDF document order (per file), then by tool number for legacy rows.
    /// </summary>
    public static List<ToolingRecord> SortByPdfSequence(IEnumerable<ToolingRecord> records) =>
        records
            .OrderBy(r => r.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.PdfRowOrder > 0 ? r.PdfRowOrder : int.MaxValue)
            .ThenBy(r => ToolNoSortKey(r.ToolNo))
            .ThenBy(r => r.Id)
            .ToList();

    public static int ToolNoSortKey(string? toolNo)
    {
        if (string.IsNullOrWhiteSpace(toolNo))
            return int.MaxValue;

        var text = toolNo.Trim();
        var m = ToolNoKeyRegex.Match(text);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var tNum))
            return tNum;

        if (int.TryParse(text, out var numeric))
            return numeric;

        return int.MaxValue - 1;
    }
}
