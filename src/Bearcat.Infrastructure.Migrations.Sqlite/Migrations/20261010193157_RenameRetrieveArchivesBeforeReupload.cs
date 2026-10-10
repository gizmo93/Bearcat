using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class RenameRetrieveArchivesBeforeReupload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RetrieveArchivesBeforeReupload",
                table: "ArchiveStorageFolders",
                newName: "UseLocalWorkingCopyForReuploads"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UseLocalWorkingCopyForReuploads",
                table: "ArchiveStorageFolders",
                newName: "RetrieveArchivesBeforeReupload"
            );
        }
    }
}
