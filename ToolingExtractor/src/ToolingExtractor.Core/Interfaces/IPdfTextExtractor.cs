namespace ToolingExtractor.Core.Interfaces;

public interface IPdfTextExtractor
{
    Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default);
}
