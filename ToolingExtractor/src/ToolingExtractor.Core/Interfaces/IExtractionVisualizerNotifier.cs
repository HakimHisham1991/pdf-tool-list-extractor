using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Core.Interfaces;

public interface IExtractionVisualizerNotifier
{
    void BeginFile(int jobId, string filePath, string displayName);
    void SetStage(
        int jobId,
        string stage,
        string message,
        int pageIndex,
        int pageCount,
        int imageWidth,
        int imageHeight,
        IReadOnlyList<VisualizerHighlight>? highlights = null,
        int activeHighlightIndex = -1);
    void EndFile(int jobId);
    void ClearJob(int jobId);
}
