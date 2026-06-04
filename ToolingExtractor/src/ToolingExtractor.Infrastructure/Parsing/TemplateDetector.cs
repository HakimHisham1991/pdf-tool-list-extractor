using Microsoft.Extensions.Logging;
using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Interfaces;

namespace ToolingExtractor.Infrastructure.Parsing;

public class TemplateDetector : ITemplateDetector
{
    private readonly ILogger<TemplateDetector> _logger;

    public TemplateDetector(ILogger<TemplateDetector> logger) => _logger = logger;

    public TemplateType Detect(string extractedText)
    {
        var text = extractedText;
        var scoreA = 0;
        if (text.Contains("Tool List", StringComparison.OrdinalIgnoreCase)) scoreA++;
        if (text.Contains("Part Number:", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Part name:", StringComparison.OrdinalIgnoreCase))
            scoreA++;
        if (text.Contains("Operation:", StringComparison.OrdinalIgnoreCase) ||
            System.Text.RegularExpressions.Regex.IsMatch(text, @"\bOP\d+\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            scoreA++;
        if (text.Contains("Machine Model", StringComparison.OrdinalIgnoreCase)) scoreA++;
        if (text.Contains("Work Centre", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Workcenter", StringComparison.OrdinalIgnoreCase))
            scoreA++;
        if (text.Contains("Tool No.", StringComparison.OrdinalIgnoreCase)) scoreA++;
        if (text.Contains("Tool Name", StringComparison.OrdinalIgnoreCase)) scoreA++;
        if (text.Contains("Consumable Tool Description", StringComparison.OrdinalIgnoreCase) ||
            (text.Contains("Consumable", StringComparison.OrdinalIgnoreCase) &&
             text.Contains("Tool Description", StringComparison.OrdinalIgnoreCase)))
            scoreA++;
        if (scoreA >= 4) return TemplateType.TemplateA;

        var scoreB = 0;
        if (text.Contains("P/N:", StringComparison.OrdinalIgnoreCase)) scoreB++;
        if (text.Contains("OP:", StringComparison.OrdinalIgnoreCase)) scoreB++;
        if (text.Contains("W/C:", StringComparison.OrdinalIgnoreCase)) scoreB++;
        if (text.Contains("Tool#", StringComparison.OrdinalIgnoreCase) || text.Contains("T#", StringComparison.OrdinalIgnoreCase)) scoreB++;
        if (scoreB >= 3) return TemplateType.TemplateB;

        var scoreC = 0;
        if (text.Contains("Part No", StringComparison.OrdinalIgnoreCase)) scoreC++;
        if (text.Contains("Operation No", StringComparison.OrdinalIgnoreCase)) scoreC++;
        if (text.Contains("Cutter No", StringComparison.OrdinalIgnoreCase) || text.Contains("Cutter Description", StringComparison.OrdinalIgnoreCase)) scoreC++;
        if (scoreC >= 2) return TemplateType.TemplateC;

        _logger.LogWarning(
            "Unknown template detected (scores A={ScoreA}/7, B={ScoreB}/4, C={ScoreC}/3)",
            scoreA, scoreB, scoreC);
        return TemplateType.Unknown;
    }
}
