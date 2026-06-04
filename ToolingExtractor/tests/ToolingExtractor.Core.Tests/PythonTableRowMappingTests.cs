using ToolingExtractor.Core.Enums;
using ToolingExtractor.Infrastructure.Parsing;
using Xunit;

namespace ToolingExtractor.Core.Tests;

public class PythonTableRowMappingTests
{
    [Fact]
    public void PythonToolTableRow_MapsToCanonicalToolingFields()
    {
        var row = new PythonToolTableRow
        {
            Tool_No = "T01",
            Tool_Name = "Face Mill",
            Consumable_Tool_Description = "63mm insert mill",
            Tool_Supplier = "Sandvik",
            Tool_Identifier = "BT40-FMB22",
            Total_Diameter = "63.000",
            Flute_Length = "40.000",
            Total_Length = "120.000",
            Total_Corner_Radius = "0.000",
            Anchor_Description = "Arbor 22mm",
            Tool_Path_Time_In_Minutes = "12.5",
            Remarks = "test"
        };

        Assert.Equal("T01", row.Tool_No);
        Assert.Equal("BT40-FMB22", row.Tool_Identifier);
        Assert.Equal("63.000", row.Total_Diameter);
        Assert.Equal(TemplateType.TemplateA, TemplateType.TemplateA);
    }
}
