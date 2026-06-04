using ToolingExtractor.Core.Enums;

namespace ToolingExtractor.Core.Models;

public class ExtractionJob
{
    public int Id { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SkippedFiles { get; set; }
    public int FailedFiles { get; set; }
    public int AmendedFilesDetected { get; set; }
    public JobStatus Status { get; set; }
    public string? ErrorSummary { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
    public string TriggeringWorkstation { get; set; } = string.Empty;
    public string TriggeringIpAddress { get; set; } = string.Empty;
    public List<ToolingRecord> Records { get; set; } = new();
}
