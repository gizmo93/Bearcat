using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddArchiveStorageFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArchiveStorageFolderId",
                table: "Archives",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "ArchiveStorageFolders",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinimumFreeSpaceGb = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    RetrieveArchivesBeforeReupload = table.Column<bool>(
                        type: "INTEGER",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveStorageFolders", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Archives_ArchiveStorageFolderId",
                table: "Archives",
                column: "ArchiveStorageFolderId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveStorageFolders_Name",
                table: "ArchiveStorageFolders",
                column: "Name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveStorageFolders_Path",
                table: "ArchiveStorageFolders",
                column: "Path",
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Archives_ArchiveStorageFolders_ArchiveStorageFolderId",
                table: "Archives",
                column: "ArchiveStorageFolderId",
                principalTable: "ArchiveStorageFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Archives_ArchiveStorageFolders_ArchiveStorageFolderId",
                table: "Archives"
            );

            migrationBuilder.DropTable(name: "ArchiveStorageFolders");

            migrationBuilder.DropIndex(
                name: "IX_Archives_ArchiveStorageFolderId",
                table: "Archives"
            );

            migrationBuilder.DropColumn(name: "ArchiveStorageFolderId", table: "Archives");
        }
    }
}
