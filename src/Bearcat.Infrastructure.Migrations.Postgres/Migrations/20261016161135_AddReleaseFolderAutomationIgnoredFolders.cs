using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
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
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "IgnoreExistingOnFirstScan",
                table: "ReleaseFolderAutomations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.CreateTable(
                name: "ReleaseFolderAutomationIgnoredFolders",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseFolderAutomationId = table.Column<int>(type: "integer", nullable: false),
                    FolderPath = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: false
                    ),
                    IgnoredAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseFolderAutomationIgnoredFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseFolderAutomationIgnoredFolders_ReleaseFolderAutomati~",
                        column: x => x.ReleaseFolderAutomationId,
                        principalTable: "ReleaseFolderAutomations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseFolderAutomationIgnoredFolders_ReleaseFolderAutomati~",
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
