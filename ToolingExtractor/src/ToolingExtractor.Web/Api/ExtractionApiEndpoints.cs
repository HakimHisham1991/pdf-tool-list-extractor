using System.Security;
using Microsoft.EntityFrameworkCore;
using ToolingExtractor.Application.DTOs;
using ToolingExtractor.Application.Services;
using ToolingExtractor.Core.Models;
using ToolingExtractor.Infrastructure.Data;
using ToolingExtractor.Infrastructure.Data.Repositories;
using ToolingExtractor.Infrastructure.Export;
using ToolingExtractor.Infrastructure.Pdf;

namespace ToolingExtractor.Web.Api;

public static class ExtractionApiEndpoints
{
    public static void MapExtractionApi(this WebApplication app)
    {
        app.MapPost("/api/extraction/start", async (
            ExtractionRequestDto request,
            ExtractionPipelineService pipeline,
            ExtractionJobQueue queue,
            HttpContext http) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.FolderPath))
                    return Results.BadRequest(new { error = "Please enter a folder path." });

                var ip = http.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                var folder = request.FolderPath.Trim();
                if (request.FilePaths is not { Count: > 0 })
                    return Results.BadRequest(new { error = "Import files first, then run Extract Tooling Data." });

                var job = await pipeline.StartExtractionAsync(folder, ip);
                queue.Enqueue(job.Id, forceReextract: true, request.FilePaths);
                return Results.Ok(new { jobId = job.Id });
            }
            catch (SecurityException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (DirectoryNotFoundException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (DllNotFoundException)
            {
                return Results.BadRequest(new
                {
                    error = "PaddleOCR native libraries are not installed. Rebuild after restoring " +
                            "Sdcb.PaddleInference.runtime.win64.mkl (see README)."
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/api/files/import", async (HttpRequest request, PdfUploadService upload, CancellationToken ct) =>
        {
            try
            {
                if (!request.HasFormContentType)
                    return Results.BadRequest(new { error = "Expected file upload (multipart form)." });

                var form = await request.ReadFormAsync(ct);
                var uploads = new List<UploadedPdfFile>();
                foreach (var file in form.Files)
                {
                    if (file.Length == 0)
                        continue;
                    uploads.Add(new UploadedPdfFile
                    {
                        FileName = file.FileName,
                        Content = file.OpenReadStream(),
                        Length = file.Length
                    });
                }

                try
                {
                    var result = await upload.ImportUploadedPdfsAsync(uploads, ct);
                    return Results.Ok(new
                    {
                        count = result.Count,
                        folderPath = result.FolderPath,
                        batchId = result.BatchId,
                        files = result.Files
                    });
                }
                finally
                {
                    foreach (var u in uploads)
                        await u.DisposeAsync();
                }
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .DisableAntiforgery();

        app.MapGet("/api/files/preview/meta", (
            string folderPath,
            string relativePath,
            FolderScanService folderScan,
            PdfPagePreviewService preview) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(relativePath))
                    return Results.BadRequest(new { error = "folderPath and relativePath are required." });

                var paths = folderScan.ResolveImportedFilePaths(folderPath.Trim(), [relativePath.Trim()]);
                if (paths.Count == 0)
                    return Results.NotFound();

                return Results.Ok(new
                {
                    fileName = Path.GetFileName(paths[0]),
                    pageCount = preview.GetPageCount(paths[0])
                });
            }
            catch (SecurityException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapGet("/api/files/preview", (
            string folderPath,
            string relativePath,
            int? page,
            FolderScanService folderScan,
            PdfPagePreviewService preview) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(relativePath))
                    return Results.BadRequest(new { error = "folderPath and relativePath are required." });

                var paths = folderScan.ResolveImportedFilePaths(folderPath.Trim(), [relativePath.Trim()]);
                if (paths.Count == 0)
                    return Results.NotFound();

                var pageNum = Math.Max(1, page ?? 1);
                var bytes = preview.RenderPageJpeg(paths[0], pageNum, maxEdgePixels: 1600);
                return bytes == null || bytes.Length == 0
                    ? Results.NotFound()
                    : Results.File(bytes, "image/jpeg");
            }
            catch (SecurityException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        app.MapPost("/api/folder/import", (ImportFolderRequestDto request, FolderScanService folderScan) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.FolderPath))
                    return Results.BadRequest(new { error = "Please enter a folder path." });

                var files = folderScan.ListPdfFilesForImport(request.FolderPath.Trim());
                return Results.Ok(new { count = files.Count, files });
            }
            catch (SecurityException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (DirectoryNotFoundException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/api/extraction/status/{jobId:int}", async (int jobId, ToolingDbContext db) =>
        {
            var job = await db.ExtractionJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId);
            return job == null ? Results.NotFound() : Results.Ok(job);
        });

        app.MapGet("/api/extraction/{jobId:int}/visualizer", (int jobId, ExtractionVisualizerStore store) =>
        {
            var snap = store.GetSnapshot(jobId);
            return snap == null ? Results.NotFound() : Results.Ok(snap);
        });

        app.MapGet("/api/extraction/{jobId:int}/visualizer/highlights", (int jobId, int? page, ExtractionVisualizerStore store) =>
        {
            var snap = store.GetSnapshot(jobId);
            if (snap == null)
                return Results.NotFound();

            var pageNum = page ?? snap.PageIndex + 1;
            if (pageNum < 1)
                pageNum = 1;

            var highlights = store.GetHighlightsForPage(jobId, pageNum);
            return highlights == null
                ? Results.Ok(new ExtractionHighlights { PageNumber = pageNum, PageCount = snap.PageCount })
                : Results.Ok(highlights);
        });

        app.MapGet("/api/extraction/{jobId:int}/visualizer/preview", (
            int jobId,
            int? page,
            ExtractionVisualizerStore store,
            PdfPagePreviewService preview) =>
        {
            var path = store.GetPreviewFilePath(jobId);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return Results.NotFound();

            var snap = store.GetSnapshot(jobId);
            var pageNum = page ?? (snap != null ? snap.PageIndex + 1 : 1);
            if (pageNum < 1)
                pageNum = 1;

            var bytes = preview.RenderPageJpeg(path, pageNum);
            return bytes == null || bytes.Length == 0
                ? Results.NotFound()
                : Results.File(bytes, "image/jpeg");
        });

        app.MapGet("/api/records", async (
            ToolingRepository repo,
            string? partNumber,
            string? operation,
            string? machine,
            bool amendedOnly = false,
            bool revisionConflictOnly = false,
            int page = 1,
            int pageSize = 50) =>
        {
            var (items, total) = await repo.QueryRecordsAsync(
                partNumber, operation, machine, amendedOnly, revisionConflictOnly, page, pageSize);
            return Results.Ok(new { items, total, page, pageSize });
        });

        app.MapGet("/api/files", async (
            ToolingRepository repo,
            string? partNumber,
            string? operation,
            int page = 1,
            int pageSize = 50) =>
        {
            var (items, total) = await repo.GetProcessedFilesAsync(partNumber, operation, page, pageSize);
            return Results.Ok(new { items, total, page, pageSize });
        });

        app.MapGet("/api/files/{hash}", async (string hash, ToolingRepository repo) =>
        {
            var summary = await repo.GetProcessedFileByHashAsync(hash);
            if (summary == null)
                return Results.NotFound();
            var tools = await repo.GetToolRowsByHashAsync(hash);
            return Results.Ok(new { summary, tools });
        });

        app.MapPatch("/api/files/{hash}", async (
            string hash,
            ProcessedFileMetadataUpdate body,
            ToolingRepository repo) =>
        {
            var updated = await repo.UpdateFileMetadataAsync(hash, body);
            return updated == 0 ? Results.NotFound() : Results.Ok(new { updated });
        });

        app.MapDelete("/api/files/{hash}", async (string hash, ToolingRepository repo) =>
        {
            var removed = await repo.DeleteByHashAsync(hash);
            return removed == 0 ? Results.NotFound() : Results.Ok(new { removed });
        });

        app.MapGet("/api/export/csv", async (int? jobId, string? hash, ToolingDbContext db, CsvExportService csv) =>
        {
            var records = await GetRecordsAsync(db, jobId, hash);
            if (records.Count == 0)
                return Results.NotFound(new { error = "No tooling records found for this export." });

            var bytes = await csv.ExportAsync(records);
            return Results.File(bytes, "text/csv", BuildExportFileName(records, "csv"));
        });

        app.MapGet("/api/export/excel", async (int? jobId, string? hash, ToolingDbContext db, ExcelExportService excel) =>
        {
            var records = await GetRecordsAsync(db, jobId, hash);
            if (records.Count == 0)
                return Results.NotFound(new { error = "No tooling records found for this export." });

            var bytes = await excel.ExportAsync(records);
            return Results.File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                BuildExportFileName(records, "xlsx"));
        });

        app.MapGet("/api/dashboard", async (ToolingRepository repo) =>
            Results.Ok(await repo.GetDashboardStatsAsync()));

        app.MapPost("/api/data/reset", async (DataResetService reset) =>
        {
            await reset.ResetAllAsync();
            return Results.Ok(new { reset = true });
        });
    }

    private static async Task<List<ToolingRecord>> GetRecordsAsync(ToolingDbContext db, int? jobId, string? hash)
    {
        var q = db.ToolingRecords.AsNoTracking();
        if (jobId.HasValue)
            q = q.Where(r => r.ExtractionJobId == jobId.Value);
        if (!string.IsNullOrWhiteSpace(hash))
            q = q.Where(r => r.SourceFileHash == hash);
        return await q.ToListAsync();
    }

    private static string BuildExportFileName(IReadOnlyList<ToolingRecord> records, string extension)
    {
        var source = records.Select(r => r.SourceFile).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        var baseName = string.IsNullOrWhiteSpace(source)
            ? "tooling-records"
            : Path.GetFileNameWithoutExtension(source);
        foreach (var c in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(c, '_');
        return $"{baseName}.{extension}";
    }
}
