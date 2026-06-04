namespace ToolingExtractor.Core.Models;

public class AmendmentReport
{
    public string FilePath { get; set; } = string.Empty;
    public bool IsAmended { get; set; }
    public List<AmendmentEvidence> Evidence { get; set; } = new();
    public int AffectedPageCount { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public class AmendmentEvidence
{
    public int PageNumber { get; set; }
    public string EvidenceType { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
