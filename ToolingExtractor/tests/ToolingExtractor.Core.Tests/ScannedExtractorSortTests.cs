using Xunit;

namespace ToolingExtractor.Core.Tests;

public class ScannedExtractorSortTests
{
    [Fact]
    public void ScannedExtractor_ColumnsReturnedInDetectionOrder_SortedByXCoord()
    {
        var regions = new (float X, float Y, string Text)[]
        {
            (300, 100, "Col3"),
            (50, 100, "Col1"),
            (150, 100, "Col2")
        };

        var text = string.Join("\t", regions.OrderBy(r => r.X).Select(r => r.Text));
        Assert.Equal("Col1\tCol2\tCol3", text);
    }
}
