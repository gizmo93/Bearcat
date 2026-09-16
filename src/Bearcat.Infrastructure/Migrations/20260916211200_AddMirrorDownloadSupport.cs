using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMirrorDownloadSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Md5Hash",
                table: "UploadedFiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "UseForMirrorDownloads",
                table: "HosterRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Md5Hash", table: "UploadedFiles");

            migrationBuilder.DropColumn(
                name: "UseForMirrorDownloads",
                table: "HosterRegistrations"
            );
        }
    }
}
