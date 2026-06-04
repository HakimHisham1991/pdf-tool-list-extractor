using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Parsing;

public class ParserA : IToolingParser
{
    private readonly ILogger<ParserA> _logger;

    public ParserA(ILogger<ParserA> logger) => _logger = logger;

    public TemplateType SupportedTemplate => TemplateType.TemplateA;

    public ExtractionResult Parse(string extractedText, string sourceFile, PdfType pdfType, AmendmentReport? amendmentReport)
    {
        var header = ParseHeader(extractedText);
        var footer = ParseFooter(extractedText);
        var lines = extractedText.Split('\n');
        var headerIndex = Array.FindIndex(lines, l => l.Contains("Tool No.", StringComparison.OrdinalIgnoreCase));
        Dictionary<string, int>? dynamicMap = null;
        var headerLine = headerIndex >= 0 ? lines[headerIndex] : string.Empty;
        var headerIsCollapsedTable = headerIndex >= 0 &&
            Regex.IsMatch(headerLine, @"\bT\d{2}\b", RegexOptions.IgnoreCase);
        if (headerIndex >= 0 && !headerIsCollapsedTable)
            dynamicMap = ParserHelpers.BuildDynamicColumnMap(headerLine, _logger, sourceFile);

        var rows = headerIndex >= 0 && !headerIsCollapsedTable
            ? ParserHelpers.ParseTableRows(lines, headerIndex, _logger, sourceFile)
            : new List<string[]>();

        if (rows.Count == 0 && headerIndex >= 0)
            rows = ParserHelpers.ParseWiGroupedToolRows(lines, headerIndex, _logger, sourceFile);

        if (rows.Count == 0)
            rows = ParserHelpers.ParseInlineWiToolRows(extractedText, _logger, sourceFile);

        if (rows.Count > 0 && rows[0].Length > 0 && Regex.IsMatch(rows[0][0], @"^T\d{2}$", RegexOptions.IgnoreCase))
            dynamicMap = null;

        var records = ParserHelpers.BuildRecordsFromRows(
            rows, header, footer, sourceFile, pdfType, SupportedTemplate, amendmentReport, dynamicMap, 1.0f);

        if (records.Count == 0)
        {
            records.Add(CreateHeaderOnlyRecord(header, footer, sourceFile, pdfType, amendmentReport));
        }

        return new ExtractionResult { Header = header, Footer = footer, Records = records };
    }

    private static ToolingHeader ParseHeader(string text)
    {
        var h = new ToolingHeader();
        h.ToolListId = MatchFirst(text,
            @"Tool List:\s*(?:(?!\n\s*\w+:).)+",
            @"Tool List\s+No\.?\s*([^\t\n\r]+)");
        h.PartNumber = MatchFirst(text,
            @"Part Number:\s*([^\n\r]+)",
            @"Part name:\s*([^\t\n\r]+)");
        h.PartDescription = MatchMultiline(text,
            @"Part Description:\s*(.+?)(?=\r?\n\s*(?:Operation|Revision|Project|Machine|Work\s*centre|Workcenter|Tool List|Tool No))",
            @"Part name:\s*[^\t\n\r]+\t([^\t\n\r]+)");
        h.Operation = MatchFirst(text,
            @"Operation:\s*([^\n\r]+)",
            @"Tool List\s+No\.?\s*[^\t\n\r]+\t(OP\d+[^\t\n\r]*)");
        h.Revision = MatchFirst(text, @"Revision:\s*([^\n\r]+)", @"\b(REV\d+)\b");
        h.ProjectCode = MatchFirst(text, @"Project Code:\s*([^\n\r]+)", @"Project\s+Code\s+([^\t\n\r]+)");
        h.Machine = Match(text, @"Machine:\s*([^\n\r]+)");
        h.Workcenter = MatchFirst(text,
            @"Workcenter:\s*([^\n\r]+)",
            @"Work Centre:\s*([^\t\n\r]+)");
        h.MachineModel = MatchFirst(text,
            @"Machine Model:\s*(?:(?!\n\s*\w+:).)+",
            @"Machine\s+Model:\s*([^\t\n\r]+)");
        return h;
    }

    private static ToolingFooter ParseFooter(string text) => new()
    {
        CamProgrammer = Match(text, @"CAM Programmer:\s*([^\n\r]*)"),
        ApprovedBy = Match(text, @"Approved by:\s*([^\n\r]*)"),
        ToolRegisteredBy = Match(text, @"Tool Register(?:ed)? By:\s*([^\n\r]*)")
    };

    private static string Match(string text, string pattern) =>
        Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value.Trim();

    private static string MatchFirst(string text, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (m.Success)
                return m.Groups[1].Value.Trim();
        }
        return string.Empty;
    }

    private static string MatchMultiline(string text, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (m.Success)
                return m.Groups[1].Value.Trim();
        }
        return string.Empty;
    }

    private static ToolingRecord CreateHeaderOnlyRecord(
        ToolingHeader header, ToolingFooter footer, string sourceFile, PdfType pdfType, AmendmentReport? report)
    {
        var r = new ToolingRecord { SourceFile = sourceFile, PdfType = pdfType, TemplateType = TemplateType.TemplateA, Remarks = "PARSE_FAILED: no table rows found" };
        ParserHelpers.ApplyHeader(r, header);
        ParserHelpers.ApplyFooter(r, footer);
        r.WasAmendmentDetected = report?.IsAmended == true;
        r.AmendmentEvidenceSummary = report?.Summary;
        return r;
    }
}
