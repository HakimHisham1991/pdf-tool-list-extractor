using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolingExtractor.Application.DTOs;
using ToolingExtractor.Core.Configuration;

namespace ToolingExtractor.Application.Services;

public class PdfUploadService
{
    private readonly ToolingExtractorOptions _options;
    private readonly ILogger<PdfUploadService> _logger;

    public PdfUploadService(IOptions<ToolingExtractorOptions> options, ILogger<PdfUploadService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ImportFilesResultDto> ImportUploadedPdfsAsync(
        IReadOnlyList<UploadedPdfFile> files,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
            throw new ArgumentException("Select at least one PDF file.");

        var batchId = Guid.NewGuid().ToString("N");
        var batchDir = Path.Combine(_options.UploadStagingPath, batchId);
        Directory.CreateDirectory(batchDir);

        var imported = new List<ImportedPdfFileDto>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (file.Length == 0)
                continue;

            var originalName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(originalName))
                continue;

            if (!string.Equals(Path.GetExtension(originalName), ".pdf", StringComparison.OrdinalIgnoreCase))
                continue;

            if (file.Length > _options.MaxUploadFileBytes)
                throw new InvalidOperationException(
                    $"File '{originalName}' exceeds the maximum upload size ({_options.MaxUploadFileBytes / (1024 * 1024)} MB).");

            var safeName = MakeUniqueFileName(originalName, usedNames);
            var destPath = Path.Combine(batchDir, safeName);

            await using var output = File.Create(destPath);
            await file.Content.CopyToAsync(output, cancellationToken);

            imported.Add(new ImportedPdfFileDto
            {
                RelativePath = safeName,
                FileName = safeName
            });

            _logger.LogInformation("Uploaded {File} ({Bytes} bytes) to batch {Batch}", safeName, file.Length, batchId);
        }

        if (imported.Count == 0)
            throw new ArgumentException("No valid PDF files were selected.");

        return new ImportFilesResultDto
        {
            BatchId = batchId,
            FolderPath = Path.GetFullPath(batchDir),
            Count = imported.Count,
            Files = imported
        };
    }

    private static string MakeUniqueFileName(string fileName, HashSet<string> usedNames)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var candidate = fileName;
        var n = 1;

        while (!usedNames.Add(candidate))
        {
            n++;
            candidate = $"{baseName}_{n}{ext}";
        }

        return candidate;
    }
}

public sealed class UploadedPdfFile : IAsyncDisposable
{
    public string FileName { get; init; } = string.Empty;
    public Stream Content { get; init; } = Stream.Null;
    public long Length { get; init; }

    public ValueTask DisposeAsync()
    {
        if (Content is IAsyncDisposable asyncDisposable)
            return asyncDisposable.DisposeAsync();
        Content.Dispose();
        return ValueTask.CompletedTask;
    }
}
