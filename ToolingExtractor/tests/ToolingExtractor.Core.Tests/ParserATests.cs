using Microsoft.Extensions.Logging.Abstractions;
using ToolingExtractor.Core.Enums;
using ToolingExtractor.Infrastructure.Parsing;
using Xunit;

namespace ToolingExtractor.Core.Tests;

public class ParserATests
{
    private readonly ParserA _parser = new(NullLogger<ParserA>.Instance);

    [Fact]
    public void Parse_FullSampleText_AllFieldsExtracted()
    {
        var result = _parser.Parse(SampleData.SampleDigitalText, "sample.pdf", PdfType.Digital, null);
        Assert.Equal("AA000-279", result.Header.PartNumber);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal("T01", result.Records[0].ToolNo);
    }

    [Fact]
    public void Parse_MultiLinePartDescription_CapturedCorrectly()
    {
        var text = "Part Description: BLEED VALVE\nBODY\nOperation: OP10\n";
        var result = _parser.Parse(text, "x.pdf", PdfType.Digital, null);
        Assert.Contains("BODY", result.Header.PartDescription);
    }

    [Fact]
    public void Parse_ContinuationRowEmptyToolNo_InheritsPreviousToolNo()
    {
        var text = """
            Tool List: X
            Part Number: P1
            Operation: OP1
            Revision: R1
            Tool No.	Tool Name
            T01	Face Mill	Desc
            	Insert 2	Desc2
            """;
        var result = _parser.Parse(text, "x.pdf", PdfType.Scanned, null);
        Assert.Contains(result.Records, r => r.Remarks.Contains("CONTINUATION"));
    }

    [Fact]
    public void Parse_RawExtractedJson_PopulatedBeforeFieldMapping()
    {
        var result = _parser.Parse(SampleData.SampleDigitalText, "x.pdf", PdfType.Digital, null);
        Assert.All(result.Records, r => Assert.False(string.IsNullOrEmpty(r.RawExtractedJson)));
    }
}
