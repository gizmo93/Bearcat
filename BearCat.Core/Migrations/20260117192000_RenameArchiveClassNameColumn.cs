using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Core.Migrations
{
    /// <inheritdoc />
    public partial class RenameArchiveClassNameColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ArchiverFullClassName",
                table: "ArchiveConfigs",
                newName: "ArchiverName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ArchiverName",
                table: "ArchiveConfigs",
                newName: "ArchiverFullClassName");
        }
    }
}
