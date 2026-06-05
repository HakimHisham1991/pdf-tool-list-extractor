using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ToolingExtractor.Core.Models;
using ToolingExtractor.Infrastructure.Data;

namespace ToolingExtractor.Application.Services;

public class ExtractionHighlightService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ToolingDbContext _db;
    private readonly ExtractionVisualizerStore _visualizer;

    public ExtractionHighlightService(ToolingDbContext db, ExtractionVisualizerStore visualizer)
    {
        _db = db;
        _visualizer = visualizer;
    }

    public async Task SaveFromVisualizerAsync(
        int jobId,
        string sourceFileHash,
        string relativePath,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var pages = _visualizer.ExportAllPageHighlights(jobId, filePath);
        if (pages.Count == 0)
            return;

        var existing = await _db.FileHighlightPages
            .Where(p => p.SourceFileHash == sourceFileHash)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _db.FileHighlightPages.RemoveRange(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var now = DateTime.UtcNow;
        foreach (var page in pages)
        {
            _db.FileHighlightPages.Add(new FileHighlightPage
            {
                SourceFileHash = sourceFileHash,
                RelativePath = relativePath,
                PageNumber = page.PageNumber,
                PageCount = page.PageCount,
                ImageWidth = page.ImageWidth,
                ImageHeight = page.ImageHeight,
                BoxesJson = JsonSerializer.Serialize(page.Boxes, JsonOptions),
                UpdatedAtUtc = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ExtractionHighlights?> GetForFilePageAsync(
        string sourceFileHash,
        int pageNumber,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.FileHighlightPages.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.SourceFileHash == sourceFileHash && p.PageNumber == pageNumber,
                cancellationToken);

        if (row == null)
            return null;

        var boxes = JsonSerializer.Deserialize<List<HighlightBox>>(row.BoxesJson, JsonOptions) ?? new List<HighlightBox>();
        return new ExtractionHighlights
        {
            PageNumber = row.PageNumber,
            PageCount = row.PageCount,
            ImageWidth = row.ImageWidth,
            ImageHeight = row.ImageHeight,
            Boxes = boxes
        };
    }
}
