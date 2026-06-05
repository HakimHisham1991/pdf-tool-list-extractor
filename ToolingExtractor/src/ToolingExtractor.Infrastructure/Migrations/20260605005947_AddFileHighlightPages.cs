using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolingExtractor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileHighlightPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileHighlightPages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceFileHash = table.Column<string>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    PageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageWidth = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageHeight = table.Column<int>(type: "INTEGER", nullable: false),
                    BoxesJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileHighlightPages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileHighlightPages_RelativePath",
                table: "FileHighlightPages",
                column: "RelativePath");

            migrationBuilder.CreateIndex(
                name: "IX_FileHighlightPages_SourceFileHash_PageNumber",
                table: "FileHighlightPages",
                columns: new[] { "SourceFileHash", "PageNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileHighlightPages");
        }
    }
}
