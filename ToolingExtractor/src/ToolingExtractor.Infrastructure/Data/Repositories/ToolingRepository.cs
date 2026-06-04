using Microsoft.EntityFrameworkCore;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Data.Repositories;

public class ToolingRepository
{
    private readonly ToolingDbContext _db;

    public ToolingRepository(ToolingDbContext db) => _db = db;

    public Task<bool> HashExistsAsync(string hash, CancellationToken ct = default) =>
        _db.ToolingRecords.AnyAsync(r => r.SourceFileHash == hash, ct);

    public async Task<int> DeleteByHashAsync(string hash, CancellationToken ct = default)
    {
        var rows = await _db.ToolingRecords.Where(r => r.SourceFileHash == hash).ToListAsync(ct);
        if (rows.Count == 0)
            return 0;
        _db.ToolingRecords.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    public Task<int> CountByHashAsync(string hash, CancellationToken ct = default) =>
        _db.ToolingRecords.CountAsync(r => r.SourceFileHash == hash, ct);

    public async Task<List<string>> GetConflictingRevisionsAsync(
        string partNumber, string operation, string newRevision, CancellationToken ct = default) =>
        await _db.ToolingRecords
            .Where(r => r.PartNumber == partNumber && r.Operation == operation && r.Revision != newRevision)
            .Select(r => r.Revision)
            .Distinct()
            .ToListAsync(ct);

    public Task<(List<ToolingRecord> Items, int Total)> QueryRecordsAsync(
        string? partNumber, string? operation, string? machine,
        bool amendedOnly, bool revisionConflictOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.ToolingRecords.AsQueryable();
        if (!string.IsNullOrWhiteSpace(partNumber))
            q = q.Where(r => r.PartNumber.Contains(partNumber));
        if (!string.IsNullOrWhiteSpace(operation))
            q = q.Where(r => r.Operation.Contains(operation));
        if (!string.IsNullOrWhiteSpace(machine))
            q = q.Where(r => r.Machine.Contains(machine));
        if (amendedOnly)
            q = q.Where(r => r.WasAmendmentDetected);
        if (revisionConflictOnly)
            q = q.Where(r => r.RevisionConflictDetected);

        return QueryPagedAsync(q, page, pageSize, ct);
    }

    public Task<List<ToolingRecord>> GetByJobIdAsync(int jobId, CancellationToken ct = default) =>
        _db.ToolingRecords.Where(r => r.ExtractionJobId == jobId).ToListAsync(ct);

    public async Task<(List<ProcessedFileSummary> Items, int Total)> GetProcessedFilesAsync(
        string? partNumber,
        string? operation,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var q = _db.ToolingRecords.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(partNumber))
            q = q.Where(r => r.PartNumber.Contains(partNumber));
        if (!string.IsNullOrWhiteSpace(operation))
            q = q.Where(r => r.Operation.Contains(operation));

        // Group in memory — SQLite cannot reliably translate nested GroupBy/First projections.
        var records = await q.ToListAsync(ct);
        var grouped = records
            .GroupBy(r => r.SourceFileHash)
            .Select(g =>
            {
                var latest = g.OrderByDescending(r => r.ExtractedAt).First();
                return new ProcessedFileSummary
                {
                    SourceFileHash = g.Key,
                    SourceFile = latest.SourceFile,
                    ToolListId = latest.ToolListId,
                    PartNumber = latest.PartNumber,
                    Operation = latest.Operation,
                    Revision = latest.Revision,
                    ToolRowCount = CountToolRows(g),
                    ExtractedAt = g.Max(r => r.ExtractedAt)
                };
            })
            .OrderByDescending(f => f.ExtractedAt)
            .ToList();

        var total = grouped.Count;
        var items = grouped.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (items, total);
    }

    public Task<List<ToolingRecord>> GetToolRowsByHashAsync(string sourceFileHash, CancellationToken ct = default) =>
        _db.ToolingRecords.AsNoTracking()
            .Where(r => r.SourceFileHash == sourceFileHash)
            .OrderBy(r => r.ToolNo)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

    public async Task<ProcessedFileSummary?> GetProcessedFileByHashAsync(string sourceFileHash, CancellationToken ct = default)
    {
        var rows = await GetToolRowsByHashAsync(sourceFileHash, ct);
        if (rows.Count == 0)
            return null;
        var first = rows[0];
        return new ProcessedFileSummary
        {
            SourceFileHash = sourceFileHash,
            SourceFile = first.SourceFile,
            ToolListId = first.ToolListId,
            PartNumber = first.PartNumber,
            Operation = first.Operation,
            Revision = first.Revision,
            ToolRowCount = CountToolRows(rows),
            ExtractedAt = rows.Max(r => r.ExtractedAt)
        };
    }

    public async Task<int> UpdateFileMetadataAsync(
        string sourceFileHash,
        ProcessedFileMetadataUpdate update,
        CancellationToken ct = default)
    {
        var rows = await _db.ToolingRecords.Where(r => r.SourceFileHash == sourceFileHash).ToListAsync(ct);
        if (rows.Count == 0)
            return 0;

        foreach (var row in rows)
        {
            if (update.ToolListId != null)
                row.ToolListId = update.ToolListId;
            if (update.PartNumber != null)
                row.PartNumber = update.PartNumber;
            if (update.Operation != null)
                row.Operation = update.Operation;
            if (update.Revision != null)
                row.Revision = update.Revision;
        }

        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    private static async Task<(List<ToolingRecord> Items, int Total)> QueryPagedAsync(
        IQueryable<ToolingRecord> q, int page, int pageSize, CancellationToken ct)
    {
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.ExtractedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    private static int CountToolRows(IEnumerable<ToolingRecord> rows) =>
        rows.Count(r => !string.IsNullOrWhiteSpace(r.ToolNo) &&
            (r.Remarks == null || !r.Remarks.StartsWith("PARSE_FAILED", StringComparison.OrdinalIgnoreCase)));

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var records = _db.ToolingRecords;
        return new DashboardStats
        {
            TotalRecords = await records.CountAsync(ct),
            DigitalCount = await records.CountAsync(r => r.PdfType == Core.Enums.PdfType.Digital, ct),
            ScannedCount = await records.CountAsync(r => r.PdfType == Core.Enums.PdfType.Scanned || r.PdfType == Core.Enums.PdfType.Mixed, ct),
            AmendedCount = await records.CountAsync(r => r.WasAmendmentDetected, ct),
            RevisionConflictCount = await records.CountAsync(r => r.RevisionConflictDetected, ct)
        };
    }
}

public class DashboardStats
{
    public int TotalRecords { get; set; }
    public int DigitalCount { get; set; }
    public int ScannedCount { get; set; }
    public int AmendedCount { get; set; }
    public int RevisionConflictCount { get; set; }
    public int SkippedDedupCount { get; set; }
}
