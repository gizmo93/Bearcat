using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddConfirmedFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfirmedFolders",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    MarkerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfirmedFolders", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmedFolders_Path",
                table: "ConfirmedFolders",
                column: "Path",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ConfirmedFolders");
        }
    }
}
