using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseCollectionDetectionToTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IgnoreLanguageInReleaseCollectionName",
                table: "ReleaseTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReleaseCollectionDetectionMode",
                table: "ReleaseTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReleaseCollectionKeyTemplate",
                table: "ReleaseTemplates",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReleaseCollectionNameTemplate",
                table: "ReleaseTemplates",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReleaseCollectionPattern",
                table: "ReleaseTemplates",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseReleaseCollections",
                table: "ReleaseTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IgnoreLanguageInReleaseCollectionName",
                table: "ReleaseTemplates");

            migrationBuilder.DropColumn(
                name: "ReleaseCollectionDetectionMode",
                table: "ReleaseTemplates");

            migrationBuilder.DropColumn(
                name: "ReleaseCollectionKeyTemplate",
                table: "ReleaseTemplates");

            migrationBuilder.DropColumn(
                name: "ReleaseCollectionNameTemplate",
                table: "ReleaseTemplates");

            migrationBuilder.DropColumn(
                name: "ReleaseCollectionPattern",
                table: "ReleaseTemplates");

            migrationBuilder.DropColumn(
                name: "UseReleaseCollections",
                table: "ReleaseTemplates");
        }
    }
}
