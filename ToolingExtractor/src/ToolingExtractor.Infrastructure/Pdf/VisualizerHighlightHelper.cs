using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Pdf;

internal static class VisualizerHighlightHelper
{
    public const float LowConfidenceThreshold = 0.7f;

    public static VisualizerHighlight FromPixelRect(
        double x, double y, double w, double h, int imageWidth, int imageHeight,
        string label, string kind)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
            return new VisualizerHighlight { Label = label, Kind = kind };

        return new VisualizerHighlight
        {
            X = Math.Clamp(x / imageWidth, 0, 1),
            Y = Math.Clamp(y / imageHeight, 0, 1),
            W = Math.Clamp(w / imageWidth, 0, 1),
            H = Math.Clamp(h / imageHeight, 0, 1),
            Label = label,
            Kind = kind
        };
    }

    public static HighlightBox HighlightFromPixelRect(
        double x, double y, double w, double h, int imageWidth, int imageHeight,
        string label, string type, float confidence = 1f)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
            return new HighlightBox { Label = label, Type = type, Confidence = confidence };

        return new HighlightBox
        {
            X = (float)Math.Clamp(x / imageWidth, 0, 1),
            Y = (float)Math.Clamp(y / imageHeight, 0, 1),
            Width = (float)Math.Clamp(w / imageWidth, 0, 1),
            Height = (float)Math.Clamp(h / imageHeight, 0, 1),
            Label = label,
            Type = type,
            Confidence = confidence
        };
    }

    public static HighlightBox HighlightFromPdfRect(
        double left, double bottom, double right, double top, double pageWidth, double pageHeight,
        string label, string type, float confidence = 1f)
    {
        if (pageWidth <= 0 || pageHeight <= 0)
            return new HighlightBox { Label = label, Type = type, Confidence = confidence };

        return new HighlightBox
        {
            X = (float)Math.Clamp(left / pageWidth, 0, 1),
            Y = (float)Math.Clamp((pageHeight - top) / pageHeight, 0, 1),
            Width = (float)Math.Clamp((right - left) / pageWidth, 0, 1),
            Height = (float)Math.Clamp((top - bottom) / pageHeight, 0, 1),
            Label = label,
            Type = type,
            Confidence = confidence
        };
    }

    public static VisualizerHighlight FromPdfWord(
        double left, double bottom, double right, double top, double pageWidth, double pageHeight,
        string label, string kind)
    {
        if (pageWidth <= 0 || pageHeight <= 0)
            return new VisualizerHighlight { Label = label, Kind = kind };

        return new VisualizerHighlight
        {
            X = Math.Clamp(left / pageWidth, 0, 1),
            Y = Math.Clamp((pageHeight - top) / pageHeight, 0, 1),
            W = Math.Clamp((right - left) / pageWidth, 0, 1),
            H = Math.Clamp((top - bottom) / pageHeight, 0, 1),
            Label = label,
            Kind = kind
        };
    }

    public static void AddIgnoredBands(List<HighlightBox> boxes)
    {
        boxes.Add(new HighlightBox { X = 0, Y = 0, Width = 1, Height = 0.12f, Type = "ignored" });
        boxes.Add(new HighlightBox { X = 0, Y = 0.88f, Width = 1, Height = 0.12f, Type = "ignored" });
    }
}
