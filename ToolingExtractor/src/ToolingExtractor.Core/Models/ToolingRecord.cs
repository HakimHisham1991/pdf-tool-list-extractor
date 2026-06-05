using ToolingExtractor.Core.Enums;

namespace ToolingExtractor.Core.Models;

public class ToolingRecord
{
    public int Id { get; set; }
    public int ExtractionJobId { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public string SourceFileHash { get; set; } = string.Empty;

    public string ToolListId { get; set; } = string.Empty;
    /// <summary>Tool List No. parsed from the PDF header (distinct from filename stored in ToolListId).</summary>
    public string ToolListNumber { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string PartDescription { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public string Workcenter { get; set; } = string.Empty;
    public string MachineModel { get; set; } = string.Empty;

    /// <summary>1-based row position in the source PDF tool table.</summary>
    public int PdfRowOrder { get; set; }

    public string ToolNo { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string ConsumableToolDescription { get; set; } = string.Empty;
    public string ToolSupplier { get; set; } = string.Empty;
    public string ToolHolder { get; set; } = string.Empty;
    public string ToolDiameterD1 { get; set; } = string.Empty;
    public string FluteLengthL1 { get; set; } = string.Empty;
    public string ToolExtLengthL2 { get; set; } = string.Empty;
    public string ToolCornerRadius { get; set; } = string.Empty;
    public string ArborDescription { get; set; } = string.Empty;
    public string ToolPathTimeMinutes { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;

    public string CamProgrammer { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string ToolRegisteredBy { get; set; } = string.Empty;

    public PdfType PdfType { get; set; }
    public TemplateType TemplateType { get; set; }
    public float ConfidenceScore { get; set; } = 1.0f;
    public bool WasAmendmentDetected { get; set; }
    public string? AmendmentEvidenceSummary { get; set; }
    public bool RevisionConflictDetected { get; set; }
    public int PageOrientation { get; set; }
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    public string? RawExtractedJson { get; set; }
}
