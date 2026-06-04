using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Core.Interfaces;

public interface IToolingParser
{
    TemplateType SupportedTemplate { get; }
    ExtractionResult Parse(string extractedText, string sourceFile, PdfType pdfType, AmendmentReport? amendmentReport);
}
