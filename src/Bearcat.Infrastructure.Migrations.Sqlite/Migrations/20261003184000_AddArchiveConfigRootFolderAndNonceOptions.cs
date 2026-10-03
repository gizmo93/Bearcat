using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddArchiveConfigRootFolderAndNonceOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreateNonceFile",
                table: "ArchiveConfigTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "PackReleaseFolderAsRootFolder",
                table: "ArchiveConfigTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "CreateNonceFile",
                table: "ArchiveConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "PackReleaseFolderAsRootFolder",
                table: "ArchiveConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CreateNonceFile", table: "ArchiveConfigTemplates");

            migrationBuilder.DropColumn(
                name: "PackReleaseFolderAsRootFolder",
                table: "ArchiveConfigTemplates"
            );

            migrationBuilder.DropColumn(name: "CreateNonceFile", table: "ArchiveConfigs");

            migrationBuilder.DropColumn(
                name: "PackReleaseFolderAsRootFolder",
                table: "ArchiveConfigs"
            );
        }
    }
}
