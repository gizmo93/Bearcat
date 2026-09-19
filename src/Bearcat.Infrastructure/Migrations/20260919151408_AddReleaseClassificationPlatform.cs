using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseClassificationPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContentTypeSource",
                table: "ReleaseClassifications",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "Platform",
                table: "ReleaseClassifications",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "PlatformSource",
                table: "ReleaseClassifications",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ContentTypeSource", table: "ReleaseClassifications");

            migrationBuilder.DropColumn(name: "Platform", table: "ReleaseClassifications");

            migrationBuilder.DropColumn(name: "PlatformSource", table: "ReleaseClassifications");
        }
    }
}
