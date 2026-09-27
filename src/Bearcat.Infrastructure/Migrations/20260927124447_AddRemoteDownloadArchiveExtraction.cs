using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteDownloadArchiveExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivesExtractedAt",
                table: "RemoteSourceDownloads",
                type: "timestamp(4) without time zone",
                precision: 4,
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "ExtractArchivesBeforeReleaseCreation",
                table: "RemoteSourceDownloads",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "ExtractArchivesBeforeReleaseCreation",
                table: "RemoteSourceAutomations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivesExtractedAt",
                table: "RemoteSourceDownloads"
            );

            migrationBuilder.DropColumn(
                name: "ExtractArchivesBeforeReleaseCreation",
                table: "RemoteSourceDownloads"
            );

            migrationBuilder.DropColumn(
                name: "ExtractArchivesBeforeReleaseCreation",
                table: "RemoteSourceAutomations"
            );
        }
    }
}
