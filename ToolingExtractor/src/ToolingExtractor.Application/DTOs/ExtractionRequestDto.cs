namespace ToolingExtractor.Application.DTOs;

public class ExtractionRequestDto
{
    public string FolderPath { get; set; } = string.Empty;
    /// <summary>Relative paths from Import Files; when set, only these PDFs are processed.</summary>
    public List<string>? FilePaths { get; set; }
}
