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

    public ExtractionJobQueue(IServiceScopeFactory scopeFactory, ILogger<ExtractionJobQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(int jobId, bool forceReextract = false, IReadOnlyList<string>? importedRelativePaths = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<ExtractionPipelineService>();
                await pipeline.ExecuteExtractionAsync(
                    jobId, forceReextract, importedRelativePaths, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background extraction failed for job {JobId}", jobId);
                await MarkJobFailedAsync(jobId, ex.Message);
            }
        });
    }

    private async Task MarkJobFailedAsync(int jobId, string message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ToolingDbContext>();
            var job = await db.ExtractionJobs.FindAsync(jobId);
            if (job == null) return;
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
