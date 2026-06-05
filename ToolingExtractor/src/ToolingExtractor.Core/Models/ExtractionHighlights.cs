namespace ToolingExtractor.Core.Models;

public class ExtractionHighlights
{
    public int PageNumber { get; set; }
    public int PageCount { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public List<HighlightBox> Boxes { get; set; } = new();
}

public class HighlightBox
{
    /// <summary>Normalized left edge (0–1, top-left origin).</summary>
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    /// <summary>ocr | table | extracted | lowconf | ignored</summary>
    public string Type { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public string Label { get; set; } = string.Empty;
}
