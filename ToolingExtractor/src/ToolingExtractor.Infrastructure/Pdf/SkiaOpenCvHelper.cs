using OpenCvSharp;
using SkiaSharp;

namespace ToolingExtractor.Infrastructure.Pdf;

internal static class SkiaOpenCvHelper
{
    public static Mat ToMat(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var mat = Cv2.ImDecode(data.ToArray(), ImreadModes.Color);
        return mat;
    }
}
