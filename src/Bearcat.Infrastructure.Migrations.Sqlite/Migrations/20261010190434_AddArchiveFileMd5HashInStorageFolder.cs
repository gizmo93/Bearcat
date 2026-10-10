using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddArchiveFileMd5HashInStorageFolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Md5HashInStorageFolder",
                table: "ArchiveFiles",
                type: "TEXT",
                maxLength: 32,
                nullable: true
            );

            migrationBuilder.Sql(
                "UPDATE \"ArchiveFiles\" SET \"Md5HashInStorageFolder\" = \"Md5Hash\" WHERE \"ArchiveId\" IN (SELECT \"Id\" FROM \"Archives\" WHERE \"ArchiveStorageFolderId\" IS NOT NULL);"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Md5HashInStorageFolder", table: "ArchiveFiles");
        }
    }
}
