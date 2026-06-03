using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumOnlyDownloadFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PremiumOnlyDownload",
                table: "Uploads",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "PremiumOnlyDownload",
                table: "UploadConfigTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "PremiumOnlyDownload",
                table: "UploadConfigs",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PremiumOnlyDownload", table: "Uploads");

            migrationBuilder.DropColumn(
                name: "PremiumOnlyDownload",
                table: "UploadConfigTemplates"
            );

            migrationBuilder.DropColumn(name: "PremiumOnlyDownload", table: "UploadConfigs");
        }
    }
}
