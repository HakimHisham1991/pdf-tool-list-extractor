using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Core.Interfaces;

public interface IPdfClassifier
{
    PdfType Classify(string filePath, out AmendmentReport? amendmentReport);
}
