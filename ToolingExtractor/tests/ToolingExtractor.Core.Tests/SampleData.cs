using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Core.Tests;

public static class SampleData
{
    public static string SampleDigitalText => """
        Tool List: AA000-279_OP10_REV00
        Part Number: AA000-279
        Part Description: BLEED VALVE
        BODY
        Operation: OP10
        Revision: REV00
        Project Code: AD01
        Machine: HW07
        Workcenter: 3X-01
        Machine Model: VCN410A-II

        Tool No.	Tool Name	Consumable Tool Description	Tool Supplier	Tool Holder	Tool Diameter (D1)	Flute Length (L1)	Tool Ext. Length (L2)	Tool CornerRadius	Arbor Description	Tool Path Time in Minutes	Remarks
        T01	Face Mill	63mm 5-Insert Face Mill	Sandvik	BT40-FMB22	63	40	120	0	Arbor 22mm	12.5	
        T02	Drill	8mm Carbide Drill	Kennametal	BT40-C20-105	8	55	110	0		8.3	Through coolant

        CAM Programmer: Ahmad Hakim
        Approved by: Razif Yusof
        Tool Register By: Hafiz Jamil
        """;

    public static List<ToolingRecord> SampleRecords => new()
    {
        new ToolingRecord
        {
            ToolListId = "AA000-279_OP10_REV00", PartNumber = "AA000-279",
            ToolNo = "T01", ToolName = "Face Mill", PdfType = PdfType.Digital,
            SourceFileHash = "abc123", WasAmendmentDetected = false,
            RevisionConflictDetected = false, ConfidenceScore = 1.0f
        },
        new ToolingRecord
        {
            ToolListId = "AA000-279_OP10_REV00", PartNumber = "AA000-279",
            ToolNo = "T02", ToolName = "Drill", PdfType = PdfType.Amended,
            SourceFileHash = "def456", WasAmendmentDetected = true,
            AmendmentEvidenceSummary = "WHITE_RECT_OVERLAP on page 1",
            RevisionConflictDetected = false, ConfidenceScore = 0.87f
        },
        new ToolingRecord
        {
            ToolListId = "AA000-279_OP10_REV01", PartNumber = "AA000-279",
            ToolNo = "T01", ToolName = "End Mill", PdfType = PdfType.Scanned,
            SourceFileHash = "ghi789", WasAmendmentDetected = false,
            RevisionConflictDetected = true, ConfidenceScore = 0.91f
        }
    };
}
