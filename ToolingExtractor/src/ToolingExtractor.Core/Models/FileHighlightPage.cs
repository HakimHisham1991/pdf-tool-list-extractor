namespace ToolingExtractor.Core.Models;

public class FileHighlightPage
{
    public int Id { get; set; }
    public string SourceFileHash { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public int PageCount { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    /// <summary>JSON array of <see cref="HighlightBox"/>.</summary>
    public string BoxesJson { get; set; } = "[]";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
