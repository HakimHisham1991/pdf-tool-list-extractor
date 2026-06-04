using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Pdf;

internal static class VisualizerHighlightHelper
{
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
}
