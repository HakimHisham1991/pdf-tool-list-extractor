using Sdcb.PaddleOCR;

namespace ToolingExtractor.Infrastructure.Pdf;

public static class OcrTableSorter
{
    public static string SortRegionsToTableText(
        IReadOnlyList<PaddleOcrResultRegion> regions, int pageHeight, int rowBucketPx = 30)
    {
        if (regions.Count == 0) return string.Empty;

        var buckets = regions
            .GroupBy(r => (int)(Math.Round(r.Rect.Center.Y / rowBucketPx) * rowBucketPx))
            .OrderBy(g => g.Key);

        return string.Join("\n", buckets.Select(bucket =>
            string.Join("\t", bucket.OrderBy(r => r.Rect.Center.X).Select(r => r.Text))));
    }
}
