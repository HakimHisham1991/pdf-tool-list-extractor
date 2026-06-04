namespace ToolingExtractor.Application.DTOs;

public class ImportFolderRequestDto
{
    public string FolderPath { get; set; } = string.Empty;
}

public class ImportedPdfFileDto
{
    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
