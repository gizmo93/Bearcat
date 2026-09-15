using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassificationSourceAndContentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContentType",
                table: "ReleaseClassifications",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "EpisodeEnd",
                table: "ReleaseClassifications",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "ReleaseGroupToken",
                table: "ReleaseClassifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "ReleaseClassifications",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "SourceSource",
                table: "ReleaseClassifications",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ContentType", table: "ReleaseClassifications");

            migrationBuilder.DropColumn(name: "EpisodeEnd", table: "ReleaseClassifications");

            migrationBuilder.DropColumn(name: "ReleaseGroupToken", table: "ReleaseClassifications");

            migrationBuilder.DropColumn(name: "Source", table: "ReleaseClassifications");

            migrationBuilder.DropColumn(name: "SourceSource", table: "ReleaseClassifications");
        }
    }
}
