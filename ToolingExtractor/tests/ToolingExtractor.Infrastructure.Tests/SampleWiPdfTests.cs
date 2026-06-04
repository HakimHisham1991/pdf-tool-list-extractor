using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ToolingExtractor.Core.Configuration;
using ToolingExtractor.Infrastructure.Parsing;
using ToolingExtractor.Infrastructure.Pdf;
using Xunit;

namespace ToolingExtractor.Infrastructure.Tests;

public class SampleWiPdfTests
{
    private const string SamplePath = @"C:\Tools\PDFs\TYPE_1_F57551907200 OP10 REV01_WI.pdf";

    [Fact]
    public void SampleWiPdf_WhenPresent_IsNotFalsePositiveAmended_AndMatchesTemplateA()
    {
        if (!File.Exists(SamplePath))
            return;

        var options = Options.Create(new ToolingExtractorOptions());
        var detector = new AmendmentDetector(options, NullLogger<AmendmentDetector>.Instance);
        var report = detector.Inspect(SamplePath);
        Assert.False(report.IsAmended);

        var classifier = new PdfClassifier(detector, options, NullLogger<PdfClassifier>.Instance);
        var pdfType = classifier.Classify(SamplePath, out _);
        Assert.True(pdfType is Core.Enums.PdfType.Digital or Core.Enums.PdfType.Mixed);

        var digital = new DigitalPdfExtractor();
        var text = digital.ExtractTextAsync(SamplePath).GetAwaiter().GetResult();
        Assert.True(text.Length > 200);

        var template = new TemplateDetector(NullLogger<TemplateDetector>.Instance).Detect(text);
        Assert.True(
            template == Core.Enums.TemplateType.TemplateA,
            $"Expected TemplateA but got {template}. Text preview:{Environment.NewLine}{text[..Math.Min(1200, text.Length)]}");

        var lines = text.Split('\n');
        var headerIndex = Array.FindIndex(lines, l => l.Contains("Tool No.", StringComparison.OrdinalIgnoreCase));
        var tLineCount = lines.Count(l => Regex.IsMatch(l, @"\bT\d+\b", RegexOptions.IgnoreCase));
        var parser = new ParserA(NullLogger<ParserA>.Instance);
        var result = parser.Parse(text, SamplePath, pdfType, report);
        var toolNos = result.Records.Where(r => !string.IsNullOrWhiteSpace(r.ToolNo)).Select(r => r.ToolNo).ToList();
        Assert.True(
            toolNos.Count >= 10,
            $"Expected >= 10 tool rows, got {toolNos.Count}. headerIndex={headerIndex}, linesWithT##={tLineCount}, " +
            $"sampleT={string.Join(" | ", lines.Where(l => Regex.IsMatch(l, @"\bT01\b")).Take(2))}");
    }
}
