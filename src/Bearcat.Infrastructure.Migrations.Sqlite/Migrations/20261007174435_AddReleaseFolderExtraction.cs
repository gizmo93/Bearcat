using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddReleaseFolderExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractionErrorMessage",
                table: "ReleaseFolderObservations",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "ExtractionFailedAt",
                table: "ReleaseFolderObservations",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "ExtractArchivesBeforeReleaseCreation",
                table: "ReleaseFolderAutomations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractionErrorMessage",
                table: "ReleaseFolderObservations"
            );

            migrationBuilder.DropColumn(
                name: "ExtractionFailedAt",
                table: "ReleaseFolderObservations"
            );

            migrationBuilder.DropColumn(
                name: "ExtractArchivesBeforeReleaseCreation",
                table: "ReleaseFolderAutomations"
            );
        }
    }
}
