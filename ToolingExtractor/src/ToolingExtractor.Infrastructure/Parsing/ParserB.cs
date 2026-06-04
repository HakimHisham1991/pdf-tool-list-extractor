using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Parsing;

public class ParserB : IToolingParser
{
    private readonly ILogger<ParserB> _logger;

    public ParserB(ILogger<ParserB> logger) => _logger = logger;

    public TemplateType SupportedTemplate => TemplateType.TemplateB;

    public ExtractionResult Parse(string extractedText, string sourceFile, PdfType pdfType, AmendmentReport? amendmentReport)
    {
        var header = new ToolingHeader
        {
            PartNumber = Match(extractedText, @"P/N:\s*([^\n\r]+)"),
            Operation = Match(extractedText, @"OP:\s*([^\n\r]+)"),
            Workcenter = Match(extractedText, @"W/C:\s*([^\n\r]+)"),
            Machine = Match(extractedText, @"Mach:\s*([^\n\r]+)"),
            MachineModel = Match(extractedText, @"Model:\s*(?:(?!\n\s*\w+:).)+"),
            ToolListId = Match(extractedText, @"Tool List:\s*([^\n\r]+)")
        };

        var footer = new ToolingFooter
        {
            CamProgrammer = Match(extractedText, @"CAM:\s*([^\n\r]*)"),
            ApprovedBy = Match(extractedText, @"Approved:\s*([^\n\r]*)")
        };

        var lines = extractedText.Split('\n');
        var headerIndex = Array.FindIndex(lines, l =>
            l.Contains("Tool#", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("T#", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Tool No", StringComparison.OrdinalIgnoreCase));

        var dynamicMap = headerIndex >= 0
            ? ParserHelpers.BuildDynamicColumnMap(lines[headerIndex], _logger, sourceFile)
            : null;

        var rows = headerIndex >= 0
            ? ParserHelpers.ParseTableRows(lines, headerIndex, _logger, sourceFile)
            : new List<string[]>();

        var records = ParserHelpers.BuildRecordsFromRows(
            rows, header, footer, sourceFile, pdfType, SupportedTemplate, amendmentReport, dynamicMap, 1.0f);

        if (records.Count == 0)
        {
            var r = new ToolingRecord { SourceFile = sourceFile, PdfType = pdfType, TemplateType = SupportedTemplate, Remarks = "PARSE_FAILED: no table rows found" };
            ParserHelpers.ApplyHeader(r, header);
            ParserHelpers.ApplyFooter(r, footer);
            records.Add(r);
        }

        return new ExtractionResult { Header = header, Footer = footer, Records = records };
    }

    private static string Match(string text, string pattern) =>
        Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value.Trim();
}
