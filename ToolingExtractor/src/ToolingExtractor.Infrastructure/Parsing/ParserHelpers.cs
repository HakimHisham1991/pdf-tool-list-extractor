using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Parsing;

internal static class ParserHelpers
{
    private static readonly Regex ToolRowStartRegex = new(
        @"^\s*(?:T\d{2,3}|\d{2})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ToolRowDimensionRegex = new(
        @"\d+\.\d{3}",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, string> ColumnAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tool no"] = nameof(ToolingRecord.ToolNo),
        ["tool name"] = nameof(ToolingRecord.ToolName),
        ["consumable"] = nameof(ToolingRecord.ConsumableToolDescription),
        ["supplier"] = nameof(ToolingRecord.ToolSupplier),
        ["tool identifier"] = nameof(ToolingRecord.ToolHolder),
        ["identifier"] = nameof(ToolingRecord.ToolHolder),
        ["holder"] = nameof(ToolingRecord.ToolHolder),
        ["total diameter"] = nameof(ToolingRecord.ToolDiameterD1),
        ["diameter"] = nameof(ToolingRecord.ToolDiameterD1),
        ["flute"] = nameof(ToolingRecord.FluteLengthL1),
        ["total length"] = nameof(ToolingRecord.ToolExtLengthL2),
        ["ext. length"] = nameof(ToolingRecord.ToolExtLengthL2),
        ["ext length"] = nameof(ToolingRecord.ToolExtLengthL2),
        ["total corner"] = nameof(ToolingRecord.ToolCornerRadius),
        ["corner"] = nameof(ToolingRecord.ToolCornerRadius),
        ["anchor"] = nameof(ToolingRecord.ArborDescription),
        ["arbor"] = nameof(ToolingRecord.ArborDescription),
        ["path time"] = nameof(ToolingRecord.ToolPathTimeMinutes),
        ["remarks"] = nameof(ToolingRecord.Remarks)
    };

    public static Dictionary<string, int>? BuildDynamicColumnMap(string headerLine, ILogger logger, string sourceFile)
    {
        var parts = headerLine.Split('\t');
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < parts.Length; i++)
        {
            var normalized = Regex.Replace(parts[i].Trim().ToLowerInvariant(), @"\s+", " ");
            foreach (var alias in ColumnAliases)
            {
                if (normalized.Contains(alias.Key, StringComparison.OrdinalIgnoreCase))
                {
                    map[alias.Value] = i;
                    break;
                }
            }
        }

        if (map.Count >= 3) return map;
        logger.LogWarning("HEADER REMAP FAILED for {File} — using positional map. Template may have drifted.", sourceFile);
        return null;
    }

    public static void ApplyTokensToRecord(ToolingRecord record, string[] tokens, Dictionary<string, int>? dynamicMap)
    {
        record.RawExtractedJson = JsonSerializer.Serialize(tokens);
        OcrTextCorrector.NormalizeDecimalSeparators(record);

        string Get(string field, int fallbackIndex)
        {
            if (dynamicMap != null && dynamicMap.TryGetValue(field, out var idx))
                return idx < tokens.Length ? tokens[idx].Trim() : string.Empty;
            return fallbackIndex < tokens.Length ? tokens[fallbackIndex].Trim() : string.Empty;
        }

        record.ToolNo = Get(nameof(ToolingRecord.ToolNo), 0);
        record.ToolName = Get(nameof(ToolingRecord.ToolName), 1);
        record.ConsumableToolDescription = Get(nameof(ToolingRecord.ConsumableToolDescription), 2);
        record.ToolSupplier = Get(nameof(ToolingRecord.ToolSupplier), 3);
        record.ToolHolder = Get(nameof(ToolingRecord.ToolHolder), 4);
        record.ToolDiameterD1 = Get(nameof(ToolingRecord.ToolDiameterD1), 5);
        record.FluteLengthL1 = Get(nameof(ToolingRecord.FluteLengthL1), 6);
        record.ToolExtLengthL2 = Get(nameof(ToolingRecord.ToolExtLengthL2), 7);
        record.ToolCornerRadius = Get(nameof(ToolingRecord.ToolCornerRadius), 8);
        record.ArborDescription = Get(nameof(ToolingRecord.ArborDescription), 9);
        record.ToolPathTimeMinutes = Get(nameof(ToolingRecord.ToolPathTimeMinutes), 10);
        record.Remarks = Get(nameof(ToolingRecord.Remarks), 11);
    }

    public static List<string[]> ParseTableRows(
        string[] lines, int headerIndex, ILogger logger, string sourceFile)
    {
        var rows = new List<string[]>();
        var footerRegex = new Regex(@"^\s*(CAM Programmer|Approved by|Tool Register)", RegexOptions.IgnoreCase);
        var sepRegex = new Regex(@"^[-=_|]{3,}$");

        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (footerRegex.IsMatch(line)) break;
            if (sepRegex.IsMatch(line.Trim())) continue;

            var tokens = line.Split('\t');
            if (tokens.Length < 3 && i + 1 < lines.Length)
            {
                var nextTokens = lines[i + 1].Split('\t');
                if (nextTokens.Length < 3)
                {
                    var merged = (line + " " + lines[i + 1]).Split('\t');
                    if (merged.Length >= 3)
                    {
                        rows.Add(merged);
                        i++;
                        continue;
                    }
                    logger.LogInformation("SKIP: row '{Raw}' — too few tokens after merge (file: {File})", line, sourceFile);
                    continue;
                }
            }

            if (tokens.Length >= 3)
                rows.Add(tokens);
            else
                logger.LogInformation("SKIP: row '{Raw}' — too few tokens (file: {File})", line, sourceFile);
        }

        return rows;
    }

    /// <summary>
    /// Master Tooling List (WI) rows often span multiple extracted lines per tool (T01, T02, …).
    /// </summary>
    public static List<string[]> ParseWiGroupedToolRows(
        string[] lines, int headerIndex, ILogger logger, string sourceFile)
    {
        var rows = new List<string[]>();
        var toolStart = ToolRowStartRegex;
        var footerRegex = new Regex(@"^\s*(CAM Programmer|Approved by|Tool Register)", RegexOptions.IgnoreCase);
        var buffer = new List<string>();

        void Flush()
        {
            if (buffer.Count == 0)
                return;
            var tokens = buffer
                .SelectMany(l => l.Split('\t', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();
            if (tokens.Length >= 4)
                rows.Add(tokens);
            else
                logger.LogInformation("SKIP WI group ({Count} lines) — too few tokens (file: {File})", buffer.Count, sourceFile);
            buffer.Clear();
        }

        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (footerRegex.IsMatch(line))
                break;

            if (toolStart.IsMatch(line))
            {
                Flush();
                buffer.Add(line);
            }
            else if (buffer.Count > 0)
                buffer.Add(line);
        }

        Flush();
        return rows;
    }

    /// <summary>
    /// Landscape WI PDFs often collapse the whole tool table into one or two tab-separated lines.
    /// Split on tool numbers (T01, T02, …) and tokenize each segment.
    /// </summary>
    public static List<string[]> ParseInlineWiToolRows(string text, ILogger logger, string sourceFile)
    {
        var rows = new List<string[]>();
        var footerIdx = IndexOfFooter(text);
        var body = footerIdx > 0 ? text[..footerIdx] : text;

        var matches = Regex.Matches(body, @"\b(?:T\d{2,3}|\d{2})\b", RegexOptions.IgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < matches.Count; i++)
        {
            var toolNo = matches[i].Value;
            if (!seen.Add(toolNo))
                continue;

            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : body.Length;
            var segment = body[start..end].Trim();
            if (segment.Contains("CAM Programmer", StringComparison.OrdinalIgnoreCase) ||
                segment.Contains("Approved by", StringComparison.OrdinalIgnoreCase) ||
                segment.Contains("Tool Register", StringComparison.OrdinalIgnoreCase))
                break;

            // Real tool rows include numeric dimensions (e.g. 63.000); skip label-only T## mentions.
            if (!Regex.IsMatch(segment, @"\d+\.\d{3}"))
                continue;

            var tokens = segment.Split('\t', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 6)
            {
                tokens = Regex.Split(segment, @"\s{2,}")
                    .Select(t => t.Trim())
                    .Where(t => t.Length > 0)
                    .ToArray();
            }

            if (tokens.Length >= 6)
                rows.Add(tokens);
            else
                logger.LogInformation("SKIP inline WI segment for {Tool} — {Count} tokens (file: {File})",
                    toolNo, tokens.Length, sourceFile);
        }

        return rows;
    }

    /// <summary>
    /// SECO-stamped Master Tooling Lists often use numeric tool numbers (10, 11, …) on one space-delimited line per tool.
    /// </summary>
    public static List<string[]> ParseSpaceDelimitedToolRows(
        string[] lines, int headerIndex, ILogger logger, string sourceFile)
    {
        var rows = new List<string[]>();
        var footerRegex = new Regex(@"^\s*(CAM Programmer|Approved by|Tool Register)", RegexOptions.IgnoreCase);

        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (footerRegex.IsMatch(line))
                break;

            if (line.Contains("Tool No", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("(D1)", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Diameter", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Flute", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Path Time", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!ToolRowStartRegex.IsMatch(line) || !ToolRowDimensionRegex.IsMatch(line))
                continue;

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 6)
                rows.Add(tokens);
            else
                logger.LogInformation("SKIP space-delimited row '{Raw}' — {Count} tokens (file: {File})",
                    line, tokens.Length, sourceFile);
        }

        return rows;
    }

    public static List<ToolingRecord> BuildRecordsFromRows(
        List<string[]> rows,
        ToolingHeader header,
        ToolingFooter footer,
        string sourceFile,
        PdfType pdfType,
        TemplateType templateType,
        AmendmentReport? amendmentReport,
        Dictionary<string, int>? dynamicMap,
        float confidenceScore)
    {
        var records = new List<ToolingRecord>();
        string lastValidToolNo = string.Empty;
        string lastValidToolName = string.Empty;

        foreach (var tokens in rows)
        {
            var record = new ToolingRecord
            {
                SourceFile = sourceFile,
                PdfType = pdfType,
                TemplateType = templateType,
                ConfidenceScore = confidenceScore,
                WasAmendmentDetected = amendmentReport?.IsAmended == true,
                AmendmentEvidenceSummary = amendmentReport?.Summary
            };

            ApplyHeader(record, header);
            ApplyFooter(record, footer);
            ApplyTokensToRecord(record, tokens, dynamicMap);

            if (string.IsNullOrWhiteSpace(record.ToolNo))
            {
                if (!string.IsNullOrWhiteSpace(lastValidToolNo))
                {
                    record.ToolNo = lastValidToolNo;
                    record.ToolName = string.IsNullOrWhiteSpace(record.ToolName) ? lastValidToolName : record.ToolName;
                    record.Remarks = (record.Remarks + " [CONTINUATION ROW]").Trim();
                }
                else
                    continue;
            }
            else if (!IsHeaderRow(record.ToolNo))
            {
                lastValidToolNo = record.ToolNo;
                lastValidToolName = record.ToolName;
            }
            else
                continue;

            records.Add(record);
        }

        return records;
    }

    private static bool IsHeaderRow(string toolNo) =>
        toolNo.Contains("Tool No", StringComparison.OrdinalIgnoreCase);

    private static int IndexOfFooter(string text)
    {
        var markers = new[] { "CAM Programmer", "Approved by", "Tool Register By", "Tool Register" };
        var idx = -1;
        foreach (var m in markers)
        {
            var i = text.IndexOf(m, StringComparison.OrdinalIgnoreCase);
            if (i >= 0 && (idx < 0 || i < idx))
                idx = i;
        }
        return idx;
    }

    public static void ApplyHeader(ToolingRecord record, ToolingHeader header)
    {
        record.ToolListId = header.ToolListId;
        record.ToolListNumber = header.ToolListId;
        record.PartNumber = header.PartNumber;
        record.PartDescription = header.PartDescription;
        record.Operation = header.Operation;
        record.Revision = header.Revision;
        record.ProjectCode = header.ProjectCode;
        record.Machine = header.Machine;
        record.Workcenter = header.Workcenter;
        record.MachineModel = header.MachineModel;
    }

    public static void ApplyFooter(ToolingRecord record, ToolingFooter footer)
    {
        record.CamProgrammer = footer.CamProgrammer;
        record.ApprovedBy = footer.ApprovedBy;
        record.ToolRegisteredBy = footer.ToolRegisteredBy;
    }
}
