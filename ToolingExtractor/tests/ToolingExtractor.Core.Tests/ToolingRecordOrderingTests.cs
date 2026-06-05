using ToolingExtractor.Core.Models;
using Xunit;

namespace ToolingExtractor.Core.Tests;

public class ToolingRecordOrderingTests
{
    [Fact]
    public void SortByPdfSequence_UsesPdfRowOrderWithinFile()
    {
        var records = new[]
        {
            new ToolingRecord { Id = 3, SourceFile = "a.pdf", ToolNo = "T44", PdfRowOrder = 15 },
            new ToolingRecord { Id = 1, SourceFile = "a.pdf", ToolNo = "T01", PdfRowOrder = 1 },
            new ToolingRecord { Id = 2, SourceFile = "a.pdf", ToolNo = "T02", PdfRowOrder = 2 },
        };

        var sorted = ToolingRecordOrdering.SortByPdfSequence(records);

        Assert.Equal(["T01", "T02", "T44"], sorted.Select(r => r.ToolNo).ToArray());
    }

    [Fact]
    public void SortByPdfSequence_LegacyRowsFallBackToToolNumber()
    {
        var records = new[]
        {
            new ToolingRecord { Id = 2, SourceFile = "a.pdf", ToolNo = "T44", PdfRowOrder = 0 },
            new ToolingRecord { Id = 1, SourceFile = "a.pdf", ToolNo = "T01", PdfRowOrder = 0 },
        };

        var sorted = ToolingRecordOrdering.SortByPdfSequence(records);

        Assert.Equal(["T01", "T44"], sorted.Select(r => r.ToolNo).ToArray());
    }
}
