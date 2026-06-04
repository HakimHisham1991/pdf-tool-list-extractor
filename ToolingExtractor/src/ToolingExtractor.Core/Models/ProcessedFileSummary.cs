namespace ToolingExtractor.Core.Models;

public class ProcessedFileSummary
{
    public string SourceFileHash { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public string ToolListId { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public int ToolRowCount { get; set; }
    public DateTime ExtractedAt { get; set; }
}

public class ProcessedFileMetadataUpdate
{
    public string? ToolListId { get; set; }
    public string? PartNumber { get; set; }
    public string? Operation { get; set; }
    public string? Revision { get; set; }
}
