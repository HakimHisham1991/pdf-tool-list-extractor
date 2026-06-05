using System.Reflection;
using ClosedXML.Excel;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Export;

public class ExcelExportService : IExportService
{
    public Task<byte[]> ExportAsync(IEnumerable<ToolingRecord> records, CancellationToken cancellationToken = default)
    {
        var list = ToolingRecordOrdering.SortByPdfSequence(records);
        var props = ToolingRecordExportColumns.Properties;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Tool Records");

        for (var c = 0; c < props.Count; c++)
            ws.Cell(1, c + 1).Value = props[c].Name;

        var headerRow = ws.Range(1, 1, 1, props.Count);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
        headerRow.Style.Font.FontColor = XLColor.White;

        var row = 2;
        foreach (var record in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var c = 0; c < props.Count; c++)
            {
                var value = props[c].GetValue(record);
                ws.Cell(row, c + 1).Value = ToolingRecordExportColumns.FormatCellValue(value);
            }
            row++;
        }

        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public byte[] ExportJobSummary(ExtractionJob job, IEnumerable<ToolingRecord> records) =>
        ExportAsync(records).GetAwaiter().GetResult();
}
