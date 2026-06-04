using ToolingExtractor.Core.Enums;

namespace ToolingExtractor.Application.DTOs;

public class ToolingRecordDto
{
    public int Id { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public string ToolListId { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public string ToolNo { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string ToolDiameterD1 { get; set; } = string.Empty;
    public string FluteLengthL1 { get; set; } = string.Empty;
    public string ToolSupplier { get; set; } = string.Empty;
    public PdfType PdfType { get; set; }
    public TemplateType TemplateType { get; set; }
    public float ConfidenceScore { get; set; }
    public bool WasAmendmentDetected { get; set; }
    public bool RevisionConflictDetected { get; set; }
    public string? AmendmentEvidenceSummary { get; set; }
}
