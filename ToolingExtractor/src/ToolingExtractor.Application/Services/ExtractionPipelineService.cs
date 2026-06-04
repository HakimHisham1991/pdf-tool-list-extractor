using System.Collections.Concurrent;
using System.Security.Principal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolingExtractor.Core.Configuration;
using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;
using ToolingExtractor.Infrastructure.Data;
using ToolingExtractor.Infrastructure.Data.Repositories;
using ToolingExtractor.Infrastructure.Parsing;
using ToolingExtractor.Infrastructure.Pdf;

namespace ToolingExtractor.Application.Services;

public class ExtractionPipelineService
{
    private readonly ToolingDbContext _db;
    private readonly FolderScanService _folderScan;
    private readonly FileHashService _hashService;
    private readonly IPdfClassifier _classifier;
    private readonly DigitalPdfExtractor _digitalExtractor;
    private readonly ScannedPdfExtractor _scannedExtractor;
    private readonly ITemplateDetector _templateDetector;
    private readonly ParserFactory _parserFactory;
    private readonly OcrTextCorrector _ocrCorrector;
    private readonly ToolingRepository _repository;
    private readonly ToolingExtractorOptions _options;
    private readonly ILogger<ExtractionPipelineService> _logger;

    public ExtractionPipelineService(
        ToolingDbContext db,
        FolderScanService folderScan,
        FileHashService hashService,
        IPdfClassifier classifier,
        DigitalPdfExtractor digitalExtractor,
        ScannedPdfExtractor scannedExtractor,
        ITemplateDetector templateDetector,
        ParserFactory parserFactory,
        OcrTextCorrector ocrCorrector,
        ToolingRepository repository,
        IOptions<ToolingExtractorOptions> options,
        ILogger<ExtractionPipelineService> logger)
    {
        _db = db;
        _folderScan = folderScan;
        _hashService = hashService;
        _classifier = classifier;
        _digitalExtractor = digitalExtractor;
        _scannedExtractor = scannedExtractor;
        _templateDetector = templateDetector;
        _parserFactory = parserFactory;
        _ocrCorrector = ocrCorrector;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Validates path, creates a job, and returns immediately (processing runs via <see cref="ExecuteExtractionAsync"/>).</summary>
    public async Task<ExtractionJob> StartExtractionAsync(
        string folderPath,
        string? triggeringIpAddress = null,
        CancellationToken cancellationToken = default)
    {
        _folderScan.ValidateFolderPath(folderPath);
        var canonicalFolder = Path.GetFullPath(folderPath);

        var job = new ExtractionJob
        {
            FolderPath = canonicalFolder,
            Status = JobStatus.Pending,
            TriggeredBy = GetTriggeredBy(),
            TriggeringWorkstation = Environment.MachineName,
            TriggeringIpAddress = triggeringIpAddress ?? string.Empty
        };

        _db.ExtractionJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<ExtractionJob> ExecuteExtractionAsync(
        int jobId,
        bool forceReextract = false,
        IReadOnlyList<string>? importedRelativePaths = null,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.ExtractionJobs.FindAsync([jobId], cancellationToken)
            ?? throw new InvalidOperationException($"Extraction job {jobId} not found.");

        job.Status = JobStatus.Running;
        await _db.SaveChangesAsync(cancellationToken);

        var canonicalFolder = job.FolderPath;
        List<string> files;
        if (importedRelativePaths is { Count: > 0 })
            files = _folderScan.ResolveImportedFilePaths(canonicalFolder, importedRelativePaths);
        else
            files = _folderScan.EnumeratePdfFiles(canonicalFolder);
        job.TotalFiles = files.Count;
        await _db.SaveChangesAsync(cancellationToken);

        if (files.Count == 0)
        {
            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return job;
        }

        var bag = new ConcurrentBag<ToolingRecord>();
        var batchConflicts = new ConcurrentDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var parallelism = ResolveParallelism(canonicalFolder);
        var semaphore = new SemaphoreSlim(parallelism);
        var skipped = 0;
        var processed = 0;
        var failed = 0;
        var amended = 0;
        using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var progressReporter = ReportProgressPeriodicallyAsync(
            job, () => skipped, () => processed, () => failed, () => amended, progressCts.Token);

        var tasks = files.Select(async filePath =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var fileTimeout = Math.Max(60, _options.PerFileTimeoutSeconds);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(fileTimeout));

                var relativePath = Path.GetRelativePath(canonicalFolder, filePath);
                var hash = _hashService.ComputeSha256(filePath);

                if (await _repository.HashExistsAsync(hash, timeoutCts.Token))
                {
                    if (forceReextract)
                    {
                        var removed = await _repository.DeleteByHashAsync(hash, timeoutCts.Token);
                        _logger.LogInformation(
                            "Re-extracting {File} — removed {Count} existing record(s) (hash {Hash})",
                            relativePath, removed, hash);
                    }
                    else
                    {
                        _logger.LogInformation("Skipping {File} — already extracted (hash {Hash})", relativePath, hash);
                        Interlocked.Increment(ref skipped);
                        return;
                    }
                }

                var pdfType = _classifier.Classify(filePath, out var amendmentReport);
                if (amendmentReport?.IsAmended == true)
                    Interlocked.Increment(ref amended);

                var text = await ExtractTextAsync(pdfType, filePath, timeoutCts.Token);
                var template = _templateDetector.Detect(text);
                if (template == TemplateType.Unknown)
                {
                    var reason =
                        $"No matching tooling template (A/B/C). PDF type: {pdfType}. " +
                        "Check OCR text below — empty or garbled text often means page render/OCR failed.";
                    await SaveFailedExtractionAsync(relativePath, reason, text, cancellationToken);
                    Interlocked.Increment(ref failed);
                    return;
                }

                var parser = _parserFactory.GetParser(template);
                var result = parser.Parse(text, relativePath, pdfType, amendmentReport);

                foreach (var record in result.Records)
                {
                    record.ExtractionJobId = job.Id;
                    record.SourceFile = relativePath;
                    record.SourceFileHash = hash;
                    if (pdfType != PdfType.Digital)
                        record.ConfidenceScore = Math.Min(record.ConfidenceScore, _scannedExtractor.LastMinConfidence);
                    record.PageOrientation = _scannedExtractor.LastPageOrientation;

                    await CheckRevisionConflictsAsync(record, batchConflicts, cancellationToken);
                    bag.Add(record);
                }

                Interlocked.Increment(ref processed);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                var msg =
                    $"Timed out after {Math.Max(60, _options.PerFileTimeoutSeconds)}s per file. " +
                    "Increase ToolingExtractor:PerFileTimeoutSeconds for large OCR jobs.";
                _logger.LogWarning("{Msg} File: {File}", msg, Path.GetFileName(filePath));
                AppendToErrorLog(filePath, "TIMEOUT", msg);
                Interlocked.Increment(ref failed);
            }
            catch (IOException ex) when (ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase))
            {
                var msg = $"File is locked by another application (Foxit, Adobe, etc.) — close it and re-run. File: {Path.GetFileName(filePath)}";
                _logger.LogWarning(msg);
                AppendToErrorLog(filePath, "FILE_LOCKED", msg);
                Interlocked.Increment(ref failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Processing failed for {File}", Path.GetFileName(filePath));
                AppendToErrorLog(filePath, ex.GetType().Name, ex.ToString());
                Interlocked.Increment(ref failed);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        await progressCts.CancelAsync();
        try { await progressReporter; } catch (OperationCanceledException) { }

        var allRecords = bag.ToList();
        foreach (var record in allRecords.Where(r => r.PdfType != PdfType.Digital))
            _ocrCorrector.CorrectRecord(record);

        await BulkInsertWithRetryAsync(allRecords, cancellationToken);

        job.SkippedFiles = skipped;
        job.ProcessedFiles = processed;
        job.FailedFiles = failed;
        job.AmendedFilesDetected = amended;
        job.Status = JobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public Task<ExtractionJob> RunAsync(
        string folderPath,
        string? triggeringIpAddress = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteExtractionAfterStartAsync(folderPath, triggeringIpAddress, cancellationToken);
    }

    private async Task<ExtractionJob> ExecuteExtractionAfterStartAsync(
        string folderPath, string? triggeringIpAddress, CancellationToken cancellationToken)
    {
        var job = await StartExtractionAsync(folderPath, triggeringIpAddress, cancellationToken);
        return await ExecuteExtractionAsync(job.Id, forceReextract: false, importedRelativePaths: null, cancellationToken);
    }

    private async Task ReportProgressPeriodicallyAsync(
        ExtractionJob job,
        Func<int> getSkipped,
        Func<int> getProcessed,
        Func<int> getFailed,
        Func<int> getAmended,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            job.SkippedFiles = getSkipped();
            job.ProcessedFiles = getProcessed();
            job.FailedFiles = getFailed();
            job.AmendedFilesDetected = getAmended();
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private int ResolveParallelism(string folderPath)
    {
        var isNetwork = folderPath.StartsWith(@"\\", StringComparison.Ordinal) ||
                        (Path.IsPathRooted(folderPath) &&
                         DriveInfo.GetDrives().Any(d =>
                             folderPath.StartsWith(d.Name, StringComparison.OrdinalIgnoreCase) &&
                             d.DriveType == DriveType.Network));

        if (isNetwork)
        {
            var actual = Math.Min(_options.MaxParallelFiles, _options.NetworkPathThrottleParallelism);
            _logger.LogInformation("Network path detected — throttling parallelism to {N} worker(s).", actual);
            return actual;
        }

        return _options.MaxParallelFiles;
    }

    private async Task<string> ExtractTextAsync(PdfType pdfType, string filePath, CancellationToken ct)
    {
        if (pdfType is PdfType.Digital or PdfType.Mixed)
            return await _digitalExtractor.ExtractTextAsync(filePath, ct);

        if (pdfType == PdfType.Amended)
        {
            try
            {
                var digital = await _digitalExtractor.ExtractTextAsync(filePath, ct);
                if (digital.Length >= 80)
                    return digital;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Digital extract fallback unavailable for amended {File}", filePath);
            }
        }

        return await _scannedExtractor.ExtractTextAsync(filePath, ct);
    }

    private async Task CheckRevisionConflictsAsync(
        ToolingRecord record,
        ConcurrentDictionary<string, HashSet<string>> batchConflicts,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(record.PartNumber) || string.IsNullOrWhiteSpace(record.Operation))
            return;

        var key = $"{record.PartNumber}|{record.Operation}";
        var existing = await _repository.GetConflictingRevisionsAsync(record.PartNumber, record.Operation, record.Revision, ct);

        batchConflicts.AddOrUpdate(key,
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase) { record.Revision },
            (_, set) =>
            {
                if (set.Any(r => !string.Equals(r, record.Revision, StringComparison.OrdinalIgnoreCase)))
                    existing.AddRange(set.Where(r => !string.Equals(r, record.Revision, StringComparison.OrdinalIgnoreCase)));
                set.Add(record.Revision);
                return set;
            });

        if (batchConflicts.TryGetValue(key, out var revs) &&
            revs.Any(r => !string.Equals(r, record.Revision, StringComparison.OrdinalIgnoreCase)))
        {
            var allExisting = existing.Concat(revs.Where(r => !string.Equals(r, record.Revision, StringComparison.OrdinalIgnoreCase))).Distinct().ToList();
            if (allExisting.Count > 0)
            {
                record.RevisionConflictDetected = true;
                var line = $"{DateTime.UtcNow:O} | {record.SourceFile} | {record.PartNumber} | {record.Operation} | ExistingRevs: [{string.Join(", ", allExisting)}] | NewRev: {record.Revision}";
                _logger.LogWarning("REVISION CONFLICT: {Part} {Op} — {Line}", record.PartNumber, record.Operation, line);
                await File.AppendAllTextAsync(
                    Path.Combine(_options.FailedExtractionOutputPath, "revision_conflicts.log"),
                    line + Environment.NewLine, ct);
            }
        }
    }

    private async Task BulkInsertWithRetryAsync(List<ToolingRecord> records, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                _db.ToolingRecords.AddRange(records);
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlite &&
                (sqlite.SqliteErrorCode == 5 || sqlite.SqliteErrorCode == 6))
            {
                _db.ChangeTracker.Clear();
                if (attempt == 3) throw;
                await Task.Delay(500, ct);
            }
        }
    }

    private async Task SaveFailedExtractionAsync(
        string relativePath, string reason, string text, CancellationToken ct)
    {
        Directory.CreateDirectory(_options.FailedExtractionOutputPath);
        var path = Path.Combine(_options.FailedExtractionOutputPath, Path.GetFileName(relativePath) + ".txt");
        var body = $"# {reason}{Environment.NewLine}{Environment.NewLine}{text}";
        await File.WriteAllTextAsync(path, body, ct);
        _logger.LogWarning("Template parse failed for {File}. Raw text saved to {Path}", relativePath, path);
    }

    private void AppendToErrorLog(string filePath, string type, string message)
    {
        try
        {
            Directory.CreateDirectory(_options.FailedExtractionOutputPath);
            var line = $"{DateTime.UtcNow:O} | {filePath} | {type} | {message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(_options.FailedExtractionOutputPath, "errors.log"), line);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write errors.log");
        }
    }

    private static string GetTriggeredBy()
    {
        try
        {
            return WindowsIdentity.GetCurrent().Name;
        }
        catch
        {
            return Environment.UserName;
        }
    }
}
