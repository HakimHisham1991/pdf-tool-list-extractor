using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolingExtractor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfRowOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PdfRowOrder",
                table: "ToolingRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PdfRowOrder",
                table: "ToolingRecords");
        }
    }
}
