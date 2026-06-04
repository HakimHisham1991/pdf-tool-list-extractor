using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolingExtractor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtractionJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FolderPath = table.Column<string>(type: "TEXT", nullable: false),
                    TotalFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessedFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    AmendedFilesDetected = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorSummary = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TriggeredBy = table.Column<string>(type: "TEXT", nullable: false),
                    TriggeringWorkstation = table.Column<string>(type: "TEXT", nullable: false),
                    TriggeringIpAddress = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExtractionJobId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceFile = table.Column<string>(type: "TEXT", nullable: false),
                    SourceFileHash = table.Column<string>(type: "TEXT", nullable: false),
                    ToolListId = table.Column<string>(type: "TEXT", nullable: false),
                    PartNumber = table.Column<string>(type: "TEXT", nullable: false),
                    PartDescription = table.Column<string>(type: "TEXT", nullable: false),
                    Operation = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectCode = table.Column<string>(type: "TEXT", nullable: false),
                    Machine = table.Column<string>(type: "TEXT", nullable: false),
                    Workcenter = table.Column<string>(type: "TEXT", nullable: false),
                    MachineModel = table.Column<string>(type: "TEXT", nullable: false),
                    ToolNo = table.Column<string>(type: "TEXT", nullable: false),
                    ToolName = table.Column<string>(type: "TEXT", nullable: false),
                    ConsumableToolDescription = table.Column<string>(type: "TEXT", nullable: false),
                    ToolSupplier = table.Column<string>(type: "TEXT", nullable: false),
                    ToolHolder = table.Column<string>(type: "TEXT", nullable: false),
                    ToolDiameterD1 = table.Column<string>(type: "TEXT", nullable: false),
                    FluteLengthL1 = table.Column<string>(type: "TEXT", nullable: false),
                    ToolExtLengthL2 = table.Column<string>(type: "TEXT", nullable: false),
                    ToolCornerRadius = table.Column<string>(type: "TEXT", nullable: false),
                    ArborDescription = table.Column<string>(type: "TEXT", nullable: false),
                    ToolPathTimeMinutes = table.Column<string>(type: "TEXT", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", nullable: false),
                    CamProgrammer = table.Column<string>(type: "TEXT", nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ToolRegisteredBy = table.Column<string>(type: "TEXT", nullable: false),
                    PdfType = table.Column<int>(type: "INTEGER", nullable: false),
                    TemplateType = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfidenceScore = table.Column<float>(type: "REAL", nullable: false),
                    WasAmendmentDetected = table.Column<bool>(type: "INTEGER", nullable: false),
                    AmendmentEvidenceSummary = table.Column<string>(type: "TEXT", nullable: true),
                    RevisionConflictDetected = table.Column<bool>(type: "INTEGER", nullable: false),
                    PageOrientation = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RawExtractedJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolingRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolingRecords_ExtractionJobs_ExtractionJobId",
                        column: x => x.ExtractionJobId,
                        principalTable: "ExtractionJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToolingRecords_ExtractionJobId",
                table: "ToolingRecords",
                column: "ExtractionJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolingRecords_Machine",
                table: "ToolingRecords",
                column: "Machine");

            migrationBuilder.CreateIndex(
                name: "IX_ToolingRecords_Operation",
                table: "ToolingRecords",
                column: "Operation");

            migrationBuilder.CreateIndex(
                name: "IX_ToolingRecords_PartNumber",
                table: "ToolingRecords",
                column: "PartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ToolingRecords_PartOpRev",
                table: "ToolingRecords",
                columns: new[] { "PartNumber", "Operation", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolingRecords_RevisionConflictDetected",
                table: "ToolingRecords",
                column: "RevisionConflictDetected");

            migrationBuilder.CreateIndex(
                name: "IX_ToolingRecords_SourceFileHash",
                table: "ToolingRecords",
                column: "SourceFileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ToolingRecords_ToolListId",
                table: "ToolingRecords",
                column: "ToolListId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolingRecords_WasAmendmentDetected",
                table: "ToolingRecords",
                column: "WasAmendmentDetected");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToolingRecords");

            migrationBuilder.DropTable(
                name: "ExtractionJobs");
        }
    }
}
