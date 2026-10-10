using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
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
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "ArchiveStorageFolders",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    Name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Path = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumFreeSpaceGb = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    RetrieveArchivesBeforeReupload = table.Column<bool>(
                        type: "boolean",
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
