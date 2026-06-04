namespace ToolingExtractor.Core.Models;

public class ToolingHeader
{
    public string ToolListId { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string PartDescription { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public string Workcenter { get; set; } = string.Empty;
    public string MachineModel { get; set; } = string.Empty;
}
