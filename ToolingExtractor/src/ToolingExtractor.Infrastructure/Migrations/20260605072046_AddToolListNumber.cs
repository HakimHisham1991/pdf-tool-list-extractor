using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolingExtractor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolListNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ToolListNumber",
                table: "ToolingRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ToolListNumber",
                table: "ToolingRecords");
        }
    }
}
