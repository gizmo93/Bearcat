using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddReleaseFolderAutomationIgnoredFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCompletedInitialScan",
                table: "ReleaseFolderAutomations",
                type: "INTEGER",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreExistingOnFirstScan",
                table: "ReleaseFolderAutomations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.CreateTable(
                name: "ReleaseFolderAutomationIgnoredFolders",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseFolderAutomationId = table.Column<int>(type: "INTEGER", nullable: false),
                    FolderPath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: false
                    ),
                    IgnoredAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseFolderAutomationIgnoredFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseFolderAutomationIgnoredFolders_ReleaseFolderAutomations_ReleaseFolderAutomationId",
                        column: x => x.ReleaseFolderAutomationId,
                        principalTable: "ReleaseFolderAutomations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseFolderAutomationIgnoredFolders_ReleaseFolderAutomationId_FolderPath",
                table: "ReleaseFolderAutomationIgnoredFolders",
                columns: new[] { "ReleaseFolderAutomationId", "FolderPath" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReleaseFolderAutomationIgnoredFolders");

            migrationBuilder.DropColumn(
                name: "HasCompletedInitialScan",
                table: "ReleaseFolderAutomations"
            );

            migrationBuilder.DropColumn(
                name: "IgnoreExistingOnFirstScan",
                table: "ReleaseFolderAutomations"
            );
        }
    }
}
