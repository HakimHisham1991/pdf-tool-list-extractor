using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolingExtractor.Core.Configuration;
using ToolingExtractor.Infrastructure.Data;

namespace ToolingExtractor.Application.Services;

/// <summary>Clears SQLite data and on-disk extraction logs on each full page load.</summary>
public class DataResetService
{
    private readonly ToolingDbContext _db;
    private readonly ToolingExtractorOptions _options;
    private readonly ILogger<DataResetService> _logger;

    public DataResetService(
        ToolingDbContext db,
        IOptions<ToolingExtractorOptions> options,
        ILogger<DataResetService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ResetAllAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.EnsureDeletedAsync(cancellationToken);
        await _db.Database.MigrateAsync(cancellationToken);
        ClearDirectoryContents(_options.FailedExtractionOutputPath);
        ClearDirectoryContents(_options.AmendedPdfLogPath);
        ClearDirectoryContents(_options.UploadStagingPath);
        _logger.LogInformation("Application data reset (database and log folders cleared).");
    }

    private static void ClearDirectoryContents(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        foreach (var file in Directory.EnumerateFiles(directoryPath))
        {
            try { File.Delete(file); }
            catch { /* best effort */ }
        }

        foreach (var dir in Directory.EnumerateDirectories(directoryPath))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* best effort */ }
        }
    }
}
