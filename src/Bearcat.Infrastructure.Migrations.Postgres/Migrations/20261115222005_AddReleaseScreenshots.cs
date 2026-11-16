using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
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
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "CreateScreenshots",
                table: "ReleaseTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotCount",
                table: "ReleaseTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 6
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotGridColumns",
                table: "ReleaseTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 4
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotGridRows",
                table: "ReleaseTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 4
            );

            migrationBuilder.AddColumn<bool>(
                name: "CreateScreenshotGrid",
                table: "Releases",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "CreateScreenshots",
                table: "Releases",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotCount",
                table: "Releases",
                type: "integer",
                nullable: false,
                defaultValue: 6
            );

            migrationBuilder.AddColumn<string>(
                name: "ScreenshotGenerationErrorMessage",
                table: "Releases",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotGridColumns",
                table: "Releases",
                type: "integer",
                nullable: false,
                defaultValue: 4
            );

            migrationBuilder.AddColumn<int>(
                name: "ScreenshotGridRows",
                table: "Releases",
                type: "integer",
                nullable: false,
                defaultValue: 4
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "ScreenshotsGeneratedAt",
                table: "Releases",
                type: "timestamp(4) without time zone",
                precision: 4,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "ReleaseScreenshotId",
                table: "ImageUploads",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadCover",
                table: "ImageUploadConfigTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadScreenshotGrid",
                table: "ImageUploadConfigTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadScreenshots",
                table: "ImageUploadConfigTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadCover",
                table: "ImageUploadConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadScreenshotGrid",
                table: "ImageUploadConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadScreenshots",
                table: "ImageUploadConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.CreateTable(
                name: "ReleaseScreenshots",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<TimeSpan>(type: "interval", nullable: true),
                    RelativeFilePath = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: false
                    ),
                    FileDeletedAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: true
                    ),
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
                name: "IX_ImageUploads_ReleaseScreenshotId",
                table: "ImageUploads",
                column: "ReleaseScreenshotId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseScreenshots_ReleaseId",
                table: "ReleaseScreenshots",
                column: "ReleaseId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ImageUploads_ReleaseScreenshots_ReleaseScreenshotId",
                table: "ImageUploads",
                column: "ReleaseScreenshotId",
                principalTable: "ReleaseScreenshots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImageUploads_ReleaseScreenshots_ReleaseScreenshotId",
                table: "ImageUploads"
            );

            migrationBuilder.DropTable(name: "ReleaseScreenshots");

            migrationBuilder.DropIndex(
                name: "IX_ImageUploads_ReleaseScreenshotId",
                table: "ImageUploads"
            );

            migrationBuilder.DropColumn(name: "CreateScreenshotGrid", table: "ReleaseTemplates");

            migrationBuilder.DropColumn(name: "CreateScreenshots", table: "ReleaseTemplates");

            migrationBuilder.DropColumn(name: "ScreenshotCount", table: "ReleaseTemplates");

            migrationBuilder.DropColumn(name: "ScreenshotGridColumns", table: "ReleaseTemplates");

            migrationBuilder.DropColumn(name: "ScreenshotGridRows", table: "ReleaseTemplates");

            migrationBuilder.DropColumn(name: "CreateScreenshotGrid", table: "Releases");

            migrationBuilder.DropColumn(name: "CreateScreenshots", table: "Releases");

            migrationBuilder.DropColumn(name: "ScreenshotCount", table: "Releases");

            migrationBuilder.DropColumn(
                name: "ScreenshotGenerationErrorMessage",
                table: "Releases"
            );

            migrationBuilder.DropColumn(name: "ScreenshotGridColumns", table: "Releases");

            migrationBuilder.DropColumn(name: "ScreenshotGridRows", table: "Releases");

            migrationBuilder.DropColumn(name: "ScreenshotsGeneratedAt", table: "Releases");

            migrationBuilder.DropColumn(name: "ReleaseScreenshotId", table: "ImageUploads");

            migrationBuilder.DropColumn(name: "UploadCover", table: "ImageUploadConfigTemplates");

            migrationBuilder.DropColumn(
                name: "UploadScreenshotGrid",
                table: "ImageUploadConfigTemplates"
            );

            migrationBuilder.DropColumn(
                name: "UploadScreenshots",
                table: "ImageUploadConfigTemplates"
            );

            migrationBuilder.DropColumn(name: "UploadCover", table: "ImageUploadConfigs");

            migrationBuilder.DropColumn(name: "UploadScreenshotGrid", table: "ImageUploadConfigs");

            migrationBuilder.DropColumn(name: "UploadScreenshots", table: "ImageUploadConfigs");
        }
    }
}
