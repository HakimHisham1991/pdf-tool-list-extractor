using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Core.Interfaces;

public interface IAmendmentDetector
{
    AmendmentReport Inspect(string filePath);
}
