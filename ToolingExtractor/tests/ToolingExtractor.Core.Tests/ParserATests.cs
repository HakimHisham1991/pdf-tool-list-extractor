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

    [Fact]
    public void Parse_SecoNumericToolNumbers_ExtractsAllToolRows()
    {
        const string text = """
            Master Tooling List
            Tool List No. 351-2223-3_OP20_REV05 Part name: FCL_CLEVIS_OIL-TANK-ACCESS-DOOR Project Code AH03
            Tool No. Tool Name Consumable Tool Description Tool Supplier Tool Holder Diameter (D1)
            10 SCE-MILL_Ø12.0_R-.66 JS720120D2R100.0Z6-HXT SECO NA 12.000 30.000 45.000 R1.00 HSK63AHPVTT12090M 35.00
            11 SCE-MILL_Ø12.0_R0.00 554120Z4.0-SIRON-A SECO NA 12.000 26.000 45.000 0.000 H2SK63AHPVTT1290M 1.71
            13 SCE-MILL_Ø12.0_R3.00 6289638-5F-R3XD12XCL25XTL80 KENNAMETAL NA 12.000 25.000 45.000 R3.00 HSK623AHPVTT1090M 7.52
            14 BALL_MILL_Ø6 JS534060D2B.0Z4-NXT SECO NA 6.000 30.000 45.000 R3.00 HSK63AHPVTT06090M 31.94
            CAM Programmer: Low Boon Bao
            """;

        var result = _parser.Parse(text, "351-2223-3_OP20_REV05_Tooling.pdf", PdfType.Digital, null);

        Assert.Equal("FCL_CLEVIS_OIL-TANK-ACCESS-DOOR", result.Header.PartNumber);
        Assert.Equal("OP20", result.Header.Operation);
        Assert.Equal("REV05", result.Header.Revision);
        Assert.Equal(4, result.Records.Count);
        Assert.Equal("10", result.Records[0].ToolNo);
        Assert.Equal("14", result.Records[3].ToolNo);
        Assert.Contains("SCE-MILL", result.Records[0].ToolName);
    }
}
