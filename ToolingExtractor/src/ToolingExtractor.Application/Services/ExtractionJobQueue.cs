using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToolingExtractor.Core.Enums;
using ToolingExtractor.Infrastructure.Data;

namespace ToolingExtractor.Application.Services;

public class ExtractionJobQueue
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExtractionJobQueue> _logger;
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _jobTokens = new();

    public ExtractionJobQueue(IServiceScopeFactory scopeFactory, ILogger<ExtractionJobQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(int jobId, bool forceReextract = false, IReadOnlyList<string>? importedRelativePaths = null)
    {
        var cts = new CancellationTokenSource();
        _jobTokens[jobId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<ExtractionPipelineService>();
                await pipeline.ExecuteExtractionAsync(
                    jobId, forceReextract, importedRelativePaths, cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Extraction job {JobId} was cancelled", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background extraction failed for job {JobId}", jobId);
                await MarkJobFailedAsync(jobId, ex.Message);
            }
            finally
            {
                if (_jobTokens.TryRemove(jobId, out var removed))
                    removed.Dispose();
            }
        });
    }

    public bool TryCancel(int jobId)
    {
        if (!_jobTokens.TryGetValue(jobId, out var cts))
            return false;

        cts.Cancel();
        return true;
    }

    private async Task MarkJobFailedAsync(int jobId, string message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ToolingDbContext>();
            var job = await db.ExtractionJobs.FindAsync(jobId);
            if (job == null) return;
            if (job.Status is JobStatus.Completed or JobStatus.Cancelled)
                return;
            job.Status = JobStatus.Failed;
            job.ErrorSummary = message;
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not mark job {JobId} as failed", jobId);
        }
    }
}
