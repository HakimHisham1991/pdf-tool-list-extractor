namespace ToolingExtractor.Core.Models;

public class ExtractionResult
{
    public List<ToolingRecord> Records { get; set; } = new();
    public ToolingHeader Header { get; set; } = new();
    public ToolingFooter Footer { get; set; } = new();
}
