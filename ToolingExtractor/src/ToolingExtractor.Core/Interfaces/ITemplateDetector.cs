using ToolingExtractor.Core.Enums;

namespace ToolingExtractor.Core.Interfaces;

public interface ITemplateDetector
{
    TemplateType Detect(string extractedText);
}
