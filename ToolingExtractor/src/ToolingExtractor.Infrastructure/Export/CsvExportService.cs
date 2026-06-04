using System.Reflection;
using System.Text;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Export;

public class CsvExportService : IExportService
{
    public Task<byte[]> ExportAsync(IEnumerable<ToolingRecord> records, CancellationToken cancellationToken = default)
    {
        var props = typeof(ToolingRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToList();

        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, new UTF8Encoding(true));
        writer.WriteLine(string.Join(",", props.Select(p => Quote(p.Name))));

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = props.Select(p =>
            {
                var v = p.GetValue(record);
                return Quote(v?.ToString() ?? string.Empty);
            });
            writer.WriteLine(string.Join(",", values));
        }

        writer.Flush();
        return Task.FromResult(ms.ToArray());
    }

    private static string Quote(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
