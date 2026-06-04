namespace ToolingExtractor.Application.DTOs;

public class ImportFilesResultDto
{
    public string BatchId { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public int Count { get; set; }
    public IReadOnlyList<ImportedPdfFileDto> Files { get; set; } = Array.Empty<ImportedPdfFileDto>();
}
