using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolingExtractor.Application.DTOs;
using ToolingExtractor.Core.Configuration;

namespace ToolingExtractor.Application.Services;

public class FolderScanService
{
    private readonly ToolingExtractorOptions _options;
    private readonly ILogger<FolderScanService> _logger;

    public FolderScanService(IOptions<ToolingExtractorOptions> options, ILogger<FolderScanService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public void ValidateFolderPath(string requestedPath)
    {
        string canonical;
        try
        {
            canonical = Path.GetFullPath(requestedPath);
        }
        catch (Exception ex)
        {
            throw new SecurityException($"Invalid folder path: {ex.Message}");
        }

        var stagingRoot = Path.GetFullPath(_options.UploadStagingPath);
        if (canonical.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(canonical))
                throw new DirectoryNotFoundException(
                    "The specified folder does not exist or is not accessible.");
            return;
        }

        var allowedPaths = _options.AllowedBasePaths
            .Select(p => Path.GetFullPath(p))
            .ToList();

        if (allowedPaths.Count == 0)
            throw new SecurityException("No allowed extraction paths are configured.");

        var isAllowed = allowedPaths.Any(allowed =>
            canonical.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            _logger.LogWarning("Path traversal attempt rejected. Requested: {Path}", canonical);
            throw new SecurityException(
                "The requested folder path is not in the list of allowed extraction paths. " +
                "Contact your administrator to add this path to AllowedBasePaths in appsettings.json.");
        }

        if (!Directory.Exists(canonical))
            throw new DirectoryNotFoundException(
                "The specified folder does not exist or is not accessible.");
    }

    public List<string> EnumeratePdfFiles(string folderPath)
    {
        ValidateFolderPath(folderPath);
        var canonical = Path.GetFullPath(folderPath);
        return Directory.GetFiles(canonical, "*.pdf", SearchOption.AllDirectories).ToList();
    }

    public IReadOnlyList<ImportedPdfFileDto> ListPdfFilesForImport(string folderPath)
    {
        var files = EnumeratePdfFiles(folderPath);
        var canonical = Path.GetFullPath(folderPath);
        return files
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new ImportedPdfFileDto
            {
                RelativePath = Path.GetRelativePath(canonical, f),
                FileName = Path.GetFileName(f)
            })
            .ToList();
    }

    public List<string> ResolveImportedFilePaths(string folderPath, IEnumerable<string> relativePaths)
    {
        ValidateFolderPath(folderPath);
        var canonical = Path.GetFullPath(folderPath);
        var resolved = new List<string>();

        foreach (var relative in relativePaths)
        {
            if (string.IsNullOrWhiteSpace(relative))
                continue;

            var full = Path.GetFullPath(Path.Combine(canonical, relative));
            if (!full.StartsWith(canonical, StringComparison.OrdinalIgnoreCase))
                throw new SecurityException($"Imported path is outside the folder: {relative}");

            if (!File.Exists(full))
                throw new FileNotFoundException($"PDF not found: {relative}", full);

            if (!string.Equals(Path.GetExtension(full), ".pdf", StringComparison.OrdinalIgnoreCase))
                continue;

            resolved.Add(full);
        }

        return resolved;
    }
}
