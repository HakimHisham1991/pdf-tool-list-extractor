using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Parsing;

public class ParserC : IToolingParser
{
    private readonly ILogger<ParserC> _logger;

    public ParserC(ILogger<ParserC> logger) => _logger = logger;

    public TemplateType SupportedTemplate => TemplateType.TemplateC;

    public ExtractionResult Parse(string extractedText, string sourceFile, PdfType pdfType, AmendmentReport? amendmentReport)
    {
        var normalized = extractedText.Replace(',', '\t');
        var header = new ToolingHeader
        {
            PartNumber = Match(normalized, @"Part No[.:]?\s*([^\n\r\t]+)"),
            Operation = Match(normalized, @"Operation No[.:]?\s*([^\n\r\t]+)"),
            PartDescription = Match(normalized, @"Description[.:]?\s*(?:(?!\n\s*\w+:).)+")
        };

        var footer = new ToolingFooter();
        var lines = normalized.Split('\n');
        var headerIndex = Array.FindIndex(lines, l =>
            l.Contains("Cutter", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Tool No", StringComparison.OrdinalIgnoreCase));

        var dynamicMap = headerIndex >= 0
            ? ParserHelpers.BuildDynamicColumnMap(lines[headerIndex], _logger, sourceFile)
            : null;

        var rows = headerIndex >= 0
            ? ParserHelpers.ParseTableRows(lines, headerIndex, _logger, sourceFile)
            : new List<string[]>();

        var records = ParserHelpers.BuildRecordsFromRows(
            rows, header, footer, sourceFile, pdfType, SupportedTemplate, amendmentReport, dynamicMap, 0.8f);

        if (records.Count == 0)
        {
            var r = new ToolingRecord { SourceFile = sourceFile, PdfType = pdfType, TemplateType = SupportedTemplate, ConfidenceScore = 0.8f, Remarks = "PARSE_FAILED: no table rows found" };
            ParserHelpers.ApplyHeader(r, header);
            records.Add(r);
        }

        return new ExtractionResult { Header = header, Footer = footer, Records = records };
    }

    private static string Match(string text, string pattern) =>
        Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value.Trim();
}
