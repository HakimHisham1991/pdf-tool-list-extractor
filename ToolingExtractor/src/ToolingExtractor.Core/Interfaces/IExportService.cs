using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Core.Interfaces;

public interface IExportService
{
    Task<byte[]> ExportAsync(IEnumerable<ToolingRecord> records, CancellationToken cancellationToken = default);
}
