using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddReleaseScreenshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CreateScreenshotGrid",
                table: "ReleaseTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "CreateScreenshots",
                table: "ReleaseTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotCount",
                table: "ReleaseTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotGridColumns",
                table: "ReleaseTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotGridRows",
                table: "ReleaseTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4
            );

            migrationBuilder.AddColumn<bool>(
                name: "CreateScreenshotGrid",
                table: "Releases",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "CreateScreenshots",
                table: "Releases",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotCount",
                table: "Releases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 6
            );

            migrationBuilder.AddColumn<string>(
                name: "ScreenshotGenerationErrorMessage",
                table: "Releases",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotGridColumns",
                table: "Releases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotGridRows",
                table: "Releases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 4
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "ScreenshotsGeneratedAt",
                table: "Releases",
                type: "TEXT",
                precision: 4,
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "ReleaseScreenshots",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    RelativeFilePath = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: false
                    ),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseScreenshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseScreenshots_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseScreenshots_ReleaseId",
                table: "ReleaseScreenshots",
                column: "ReleaseId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReleaseScreenshots");

            migrationBuilder.DropColumn(name: "CreateScreenshotGrid", table: "ReleaseTemplates");

            migrationBuilder.DropColumn(name: "CreateScreenshots", table: "ReleaseTemplates");

            migrationBuilder.DropColumn(name: "ScreenshotCount", table: "ReleaseTemplates");

            migrationBuilder.DropColumn(name: "ScreenshotGridColumns", table: "ReleaseTemplates");

            migrationBuilder.DropColumn(name: "ScreenshotGridRows", table: "ReleaseTemplates");

            migrationBuilder.Sql("""ALTER TABLE "Releases" DROP COLUMN "CreateScreenshotGrid" """);

            migrationBuilder.Sql("""ALTER TABLE "Releases" DROP COLUMN "CreateScreenshots" """);

            migrationBuilder.Sql("""ALTER TABLE "Releases" DROP COLUMN "ScreenshotCount" """);

            migrationBuilder.Sql(
                """ALTER TABLE "Releases" DROP COLUMN "ScreenshotGenerationErrorMessage" """
            );

            migrationBuilder.Sql("""ALTER TABLE "Releases" DROP COLUMN "ScreenshotGridColumns" """);

            migrationBuilder.Sql("""ALTER TABLE "Releases" DROP COLUMN "ScreenshotGridRows" """);

            migrationBuilder.Sql(
                """ALTER TABLE "Releases" DROP COLUMN "ScreenshotsGeneratedAt" """
            );
        }
    }
}
