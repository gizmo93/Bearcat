using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddBasePathToArchiveConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveFilesBasePath",
                table: "ArchiveConfigs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchiveFilesBasePath",
                table: "ArchiveConfigs");
        }
    }
}
