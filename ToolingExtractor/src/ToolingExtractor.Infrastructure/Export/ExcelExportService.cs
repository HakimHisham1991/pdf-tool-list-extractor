using ClosedXML.Excel;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Export;

public class ExcelExportService : IExportService
{
    public Task<byte[]> ExportAsync(IEnumerable<ToolingRecord> records, CancellationToken cancellationToken = default)
    {
        var list = records.ToList();
        using var wb = new XLWorkbook();

        var wsRecords = wb.Worksheets.Add("Tool Records");
        WriteToolRecordsSheet(wsRecords, list);

        var wsReview = wb.Worksheets.Add("Review Required");
        WriteReviewSheet(wsReview, list);

        var wsSummary = wb.Worksheets.Add("Summary");
        WriteSummarySheet(wsSummary, list);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    private static void WriteToolRecordsSheet(IXLWorksheet ws, List<ToolingRecord> records)
    {
        var headers = new[]
        {
            "SourceFile", "PartNumber", "Operation", "Revision", "ToolNo", "ToolName",
            "ToolDiameterD1", "FluteLengthL1", "ToolSupplier", "PdfType", "ConfidenceScore",
            "WasAmendmentDetected", "RevisionConflictDetected", "AmendmentEvidenceSummary"
        };

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        var headerRow = ws.Range(1, 1, 1, headers.Length);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        headerRow.Style.Font.FontColor = XLColor.White;

        var row = 2;
        foreach (var r in records)
        {
            ws.Cell(row, 1).Value = r.SourceFile;
            ws.Cell(row, 2).Value = r.PartNumber;
            ws.Cell(row, 3).Value = r.Operation;
            ws.Cell(row, 4).Value = r.Revision;
            ws.Cell(row, 5).Value = r.ToolNo;
            ws.Cell(row, 6).Value = r.ToolName;
            ws.Cell(row, 7).Value = r.ToolDiameterD1;
            ws.Cell(row, 8).Value = r.FluteLengthL1;
            ws.Cell(row, 9).Value = r.ToolSupplier;
            ws.Cell(row, 10).Value = r.PdfType.ToString();
            ws.Cell(row, 11).Value = r.ConfidenceScore;
            ws.Cell(row, 12).Value = r.WasAmendmentDetected;
            ws.Cell(row, 13).Value = r.RevisionConflictDetected;
            ws.Cell(row, 14).Value = r.AmendmentEvidenceSummary ?? "";

            if (r.WasAmendmentDetected)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3CD");
            if (r.RevisionConflictDetected)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8D7DA");
            row++;
        }

        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();
        ws.Columns().AdjustToContents();
    }

    private static void WriteReviewSheet(IXLWorksheet ws, List<ToolingRecord> records)
    {
        ws.Cell(1, 1).Value = "REVIEW REQUIRED — Records flagged for amendment or revision conflict. Verify before use.";
        var headers = new[]
        {
            "SourceFile", "PartNumber", "Operation", "Revision", "ToolNo", "ToolName",
            "WasAmendmentDetected", "RevisionConflictDetected", "AmendmentEvidenceSummary", "ConfidenceScore"
        };

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(3, c + 1).Value = headers[c];

        var headerRow = ws.Range(3, 1, 3, headers.Length);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#FF6B35");
        headerRow.Style.Font.FontColor = XLColor.White;

        var review = records.Where(r => r.WasAmendmentDetected || r.RevisionConflictDetected).ToList();
        var row = 4;
        foreach (var r in review)
        {
            ws.Cell(row, 1).Value = r.SourceFile;
            ws.Cell(row, 2).Value = r.PartNumber;
            ws.Cell(row, 3).Value = r.Operation;
            ws.Cell(row, 4).Value = r.Revision;
            ws.Cell(row, 5).Value = r.ToolNo;
            ws.Cell(row, 6).Value = r.ToolName;
            ws.Cell(row, 7).Value = r.WasAmendmentDetected;
            ws.Cell(row, 8).Value = r.RevisionConflictDetected;
            ws.Cell(row, 9).Value = r.AmendmentEvidenceSummary ?? "";
            ws.Cell(row, 10).Value = r.ConfidenceScore;
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void WriteSummarySheet(IXLWorksheet ws, List<ToolingRecord> records)
    {
        var headers = new[] { "PartNumber", "Operation", "TotalTools", "DigitalCount", "ScannedCount", "AmendedCount", "ConflictCount", "LatestRevision" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        ws.Range(1, 1, 1, headers.Length).Style.Font.FontColor = XLColor.White;

        var groups = records.GroupBy(r => new { r.PartNumber, r.Operation });
        var row = 2;
        foreach (var g in groups)
        {
            ws.Cell(row, 1).Value = g.Key.PartNumber;
            ws.Cell(row, 2).Value = g.Key.Operation;
            ws.Cell(row, 3).Value = g.Count();
            ws.Cell(row, 4).Value = g.Count(x => x.PdfType == Core.Enums.PdfType.Digital);
            ws.Cell(row, 5).Value = g.Count(x => x.PdfType is Core.Enums.PdfType.Scanned or Core.Enums.PdfType.Mixed);
            ws.Cell(row, 6).Value = g.Count(x => x.WasAmendmentDetected);
            ws.Cell(row, 7).Value = g.Count(x => x.RevisionConflictDetected);
            ws.Cell(row, 8).Value = g.Select(x => x.Revision).OrderDescending().FirstOrDefault() ?? "";
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    public byte[] ExportJobSummary(ExtractionJob job, IEnumerable<ToolingRecord> records) =>
        ExportAsync(records).GetAwaiter().GetResult();
}
