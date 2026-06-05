using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolingExtractor.Core;
using ToolingExtractor.Core.Configuration;
using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Parsing;

/// <summary>
/// Template A: Python bordered-table extraction first, then legacy text/OCR parsing.
/// </summary>
public class TemplateAParseService
{
    private readonly ParserA _parserA;
    private readonly PythonTableExtractionBridge _pythonBridge;
    private readonly PythonTableExtractionOptions _pythonOptions;
    private readonly ILogger<TemplateAParseService> _logger;
    private readonly IExtractionVisualizerNotifier? _visualizer;

    public TemplateAParseService(
        ParserA parserA,
        PythonTableExtractionBridge pythonBridge,
        IOptions<ToolingExtractorOptions> options,
        ILogger<TemplateAParseService> logger,
        IExtractionVisualizerNotifier? visualizer = null)
    {
        _parserA = parserA;
        _pythonBridge = pythonBridge;
        _pythonOptions = options.Value.PythonTableExtraction;
        _logger = logger;
        _visualizer = visualizer;
    }

    public async Task<ExtractionResult> ParseAsync(
        string absolutePdfPath,
        string extractedText,
        string sourceFile,
        PdfType pdfType,
        AmendmentReport? amendmentReport,
        CancellationToken cancellationToken = default)
    {
        if (ExtractionVisualizerScope.JobId is int jobId)
        {
            _visualizer?.SetStage(
                jobId, "table", "Running Python table extraction (pdfplumber / camelot / tabula)…",
                0, 0, 0, 0);
        }

        var python = await _pythonBridge.TryExtractAsync(absolutePdfPath, cancellationToken);
        if (python != null &&
            python.Cleaned.Count >= _pythonOptions.MinimumToolRows)
        {
            var fromPython = BuildFromPython(python, extractedText, sourceFile, pdfType, amendmentReport);
            if (fromPython.Records.Count >= _pythonOptions.MinimumToolRows)
            {
                _logger.LogInformation(
                    "Using Python {Method} table ({Count} tools) for {File}",
                    python.Method, fromPython.Records.Count, sourceFile);
                if (ExtractionVisualizerScope.JobId is int jid)
                {
                    _visualizer?.SetStage(
                        jid, "table", $"Python {python.Method}: {fromPython.Records.Count} tool rows",
                        0, 0, 0, 0);
                    PublishPythonHighlights(jid, python);
                }
                return fromPython;
            }
        }

        _logger.LogDebug("Falling back to text-based ParserA for {File}", sourceFile);
        return _parserA.Parse(extractedText, sourceFile, pdfType, amendmentReport);
    }

    public ExtractionResult Parse(
        string absolutePdfPath,
        string extractedText,
        string sourceFile,
        PdfType pdfType,
        AmendmentReport? amendmentReport,
        CancellationToken cancellationToken = default) =>
        ParseAsync(absolutePdfPath, extractedText, sourceFile, pdfType, amendmentReport, cancellationToken)
            .GetAwaiter().GetResult();

    private ExtractionResult BuildFromPython(
        PythonTableExtractionPayload python,
        string extractedText,
        string sourceFile,
        PdfType pdfType,
        AmendmentReport? amendmentReport)
    {
        var textResult = _parserA.Parse(extractedText, sourceFile, pdfType, amendmentReport);
        var header = textResult.Header;
        var footer = textResult.Footer;
        var records = new List<ToolingRecord>();

        foreach (var row in python.Cleaned)
        {
            if (string.IsNullOrWhiteSpace(row.Tool_No))
                continue;

            var record = new ToolingRecord
            {
                SourceFile = sourceFile,
                PdfType = pdfType,
                TemplateType = TemplateType.TemplateA,
                ConfidenceScore = pdfType == PdfType.Digital ? 1.0f : 0.92f,
                WasAmendmentDetected = amendmentReport?.IsAmended == true,
                AmendmentEvidenceSummary = amendmentReport?.Summary,
                ToolNo = row.Tool_No.Trim(),
                ToolName = row.Tool_Name,
                ConsumableToolDescription = row.Consumable_Tool_Description,
                ToolSupplier = row.Tool_Supplier,
                ToolHolder = row.Tool_Identifier,
                ToolDiameterD1 = row.Total_Diameter,
                FluteLengthL1 = row.Flute_Length,
                ToolExtLengthL2 = row.Total_Length,
                ToolCornerRadius = row.Total_Corner_Radius,
                ArborDescription = row.Anchor_Description,
                ToolPathTimeMinutes = row.Tool_Path_Time_In_Minutes,
                Remarks = row.Remarks?.Trim() ?? string.Empty
            };

            ParserHelpers.ApplyHeader(record, header);
            ParserHelpers.ApplyFooter(record, footer);
            records.Add(record);
        }

        if (records.Count == 0)
            return textResult;

        return new ExtractionResult { Header = header, Footer = footer, Records = records };
    }

    private void PublishPythonHighlights(int jobId, PythonTableExtractionPayload python)
    {
        if (_visualizer == null || python.Highlights.Count == 0)
            return;

        foreach (var page in python.Highlights)
        {
            if (page.Boxes.Count == 0)
                continue;

            var boxes = page.Boxes.Select(b => new HighlightBox
            {
                X = b.X,
                Y = b.Y,
                Width = b.Width,
                Height = b.Height,
                Type = string.IsNullOrWhiteSpace(b.Type) ? "table" : b.Type,
                Confidence = b.Confidence,
                Label = b.Label
            }).ToList();

            _visualizer.AddPageHighlights(jobId, page.PageNumber, boxes);
        }
    }
}
