using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkCrypterContainerSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableCaptcha",
                table: "UploadConfigLinkCrypterTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "EnableClickAndLoad",
                table: "UploadConfigLinkCrypterTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "EnableContainerDownload",
                table: "UploadConfigLinkCrypterTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "EnableCaptcha",
                table: "UploadConfigLinkCrypters",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "EnableClickAndLoad",
                table: "UploadConfigLinkCrypters",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "EnableContainerDownload",
                table: "UploadConfigLinkCrypters",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "EnableCaptcha",
                table: "LinkCrypterContainers",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "EnableClickAndLoad",
                table: "LinkCrypterContainers",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "EnableContainerDownload",
                table: "LinkCrypterContainers",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableCaptcha",
                table: "UploadConfigLinkCrypterTemplates"
            );

            migrationBuilder.DropColumn(
                name: "EnableClickAndLoad",
                table: "UploadConfigLinkCrypterTemplates"
            );

            migrationBuilder.DropColumn(
                name: "EnableContainerDownload",
                table: "UploadConfigLinkCrypterTemplates"
            );

            migrationBuilder.DropColumn(name: "EnableCaptcha", table: "UploadConfigLinkCrypters");

            migrationBuilder.DropColumn(
                name: "EnableClickAndLoad",
                table: "UploadConfigLinkCrypters"
            );

            migrationBuilder.DropColumn(
                name: "EnableContainerDownload",
                table: "UploadConfigLinkCrypters"
            );

            migrationBuilder.DropColumn(name: "EnableCaptcha", table: "LinkCrypterContainers");

            migrationBuilder.DropColumn(name: "EnableClickAndLoad", table: "LinkCrypterContainers");

            migrationBuilder.DropColumn(
                name: "EnableContainerDownload",
                table: "LinkCrypterContainers"
            );
        }
    }
}
