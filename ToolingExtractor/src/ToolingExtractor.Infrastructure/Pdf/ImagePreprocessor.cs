using Microsoft.Extensions.Options;
using SkiaSharp;
using ToolingExtractor.Core.Configuration;

namespace ToolingExtractor.Infrastructure.Pdf;

public class ImagePreprocessor
{
    private readonly ImagePreprocessingOptions _options;

    public ImagePreprocessor(IOptions<ToolingExtractorOptions> options) =>
        _options = options.Value.ImagePreprocessing;

    public static double ComputeMeanBrightness(SKBitmap source)
    {
        long sum = 0;
        var count = 0;
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var c = source.GetPixel(x, y);
                sum += (c.Red + c.Green + c.Blue) / 3;
                count++;
            }
        }
        return count == 0 ? 1.0 : sum / (double)count / 255.0;
    }

    public SKBitmap Preprocess(SKBitmap source)
    {
        var grayscale = ToGrayscale(source);
        var binarized = OtsuBinarize(grayscale);
        grayscale.Dispose();
        var deskewed = Deskew(binarized);
        binarized.Dispose();
        return deskewed;
    }

    public bool ShouldPreprocess(SKBitmap source) =>
        ComputeMeanBrightness(source) < _options.BrightnessThreshold;

    private static SKBitmap ToGrayscale(SKBitmap source)
    {
        var result = new SKBitmap(source.Width, source.Height, SKColorType.Gray8, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(result);
        using var paint = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
        {
            0.299f, 0.299f, 0.299f, 0, 0,
            0.587f, 0.587f, 0.587f, 0, 0,
            0.114f, 0.114f, 0.114f, 0, 0,
            0, 0, 0, 1, 0
        })};
        canvas.DrawBitmap(source, 0, 0, paint);
        return result;
    }

    private static SKBitmap OtsuBinarize(SKBitmap grayscale)
    {
        var histogram = new int[256];
        for (var y = 0; y < grayscale.Height; y++)
            for (var x = 0; x < grayscale.Width; x++)
                histogram[grayscale.GetPixel(x, y).Red]++;

        var total = grayscale.Width * grayscale.Height;
        var sum = 0;
        for (var i = 0; i < 256; i++) sum += i * histogram[i];

        var sumB = 0;
        var wB = 0;
        var maxVariance = 0.0;
        var threshold = 128;
        for (var t = 0; t < 256; t++)
        {
            wB += histogram[t];
            if (wB == 0) continue;
            var wF = total - wB;
            if (wF == 0) break;
            sumB += t * histogram[t];
            var mB = sumB / (double)wB;
            var mF = (sum - sumB) / (double)wF;
            var variance = wB * wF * (mB - mF) * (mB - mF);
            if (variance > maxVariance)
            {
                maxVariance = variance;
                threshold = t;
            }
        }

        var result = new SKBitmap(grayscale.Width, grayscale.Height, SKColorType.Gray8, SKAlphaType.Opaque);
        for (var y = 0; y < grayscale.Height; y++)
            for (var x = 0; x < grayscale.Width; x++)
            {
                var v = grayscale.GetPixel(x, y).Red >= threshold ? (byte)255 : (byte)0;
                result.SetPixel(x, y, new SKColor(v, v, v));
            }
        return result;
    }

    private SKBitmap Deskew(SKBitmap binarized)
    {
        var maxAngle = _options.MaxDeskewAngleDegrees;
        var bestAngle = 0.0;
        var bestScore = 0.0;

        for (var angle = -maxAngle; angle <= maxAngle; angle += 0.5)
        {
            var score = HorizontalProjectionScore(binarized, angle);
            if (score > bestScore)
            {
                bestScore = score;
                bestAngle = angle;
            }
        }

        if (Math.Abs(bestAngle) < 0.5)
            return binarized.Copy();

        return RotateBitmap(binarized, bestAngle);
    }

    private static double HorizontalProjectionScore(SKBitmap bmp, double angleDeg)
    {
        using var rotated = RotateBitmap(bmp, angleDeg);
        var profile = new int[rotated.Height];
        for (var y = 0; y < rotated.Height; y++)
            for (var x = 0; x < rotated.Width; x++)
                if (rotated.GetPixel(x, y).Red < 128)
                    profile[y]++;

        var variance = 0.0;
        var mean = profile.Average();
        foreach (var v in profile)
            variance += (v - mean) * (v - mean);
        return variance;
    }

    private static SKBitmap RotateBitmap(SKBitmap source, double angleDeg)
    {
        var radians = (float)(angleDeg * Math.PI / 180.0);
        var matrix = SKMatrix.CreateRotation(radians, source.Width / 2f, source.Height / 2f);
        var bounds = new SKRect(0, 0, source.Width, source.Height);
        matrix.MapRect(bounds);
        var dest = new SKBitmap((int)bounds.Width, (int)bounds.Height);
        using var canvas = new SKCanvas(dest);
        canvas.Clear(SKColors.White);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0);
        return dest;
    }
}
