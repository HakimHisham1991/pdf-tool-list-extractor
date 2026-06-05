using System.Reflection;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Export;

internal static class ToolingRecordExportColumns
{
    private static readonly HashSet<string> ExcludedColumns = new(StringComparer.Ordinal)
    {
        nameof(ToolingRecord.Machine)
    };

    public static IReadOnlyList<PropertyInfo> Properties { get; } =
        typeof(ToolingRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && !ExcludedColumns.Contains(p.Name))
            .ToList();

    public static string FormatCellValue(object? value) => value?.ToString() ?? string.Empty;
}
