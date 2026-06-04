namespace ToolingExtractor.Core.Models;

public class VisualizerHighlight
{
    /// <summary>0–1 normalized left (top-left origin).</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public string Label { get; set; } = string.Empty;
    /// <summary>ocr | digital | table | parse | column</summary>
    public string Kind { get; set; } = string.Empty;
}

public class ExtractionVisualizerSnapshot
{
    public int JobId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int PageIndex { get; set; }
    public int PageCount { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public int ActiveHighlightIndex { get; set; } = -1;
    public List<VisualizerHighlight> Highlights { get; set; } = new();
    public DateTime UpdatedAtUtc { get; set; }
    public bool HasPreview { get; set; }
}
