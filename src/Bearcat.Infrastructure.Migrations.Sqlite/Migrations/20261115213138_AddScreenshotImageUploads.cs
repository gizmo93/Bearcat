using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddScreenshotImageUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReleaseScreenshotId",
                table: "ImageUploads",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadCover",
                table: "ImageUploadConfigTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadScreenshotGrid",
                table: "ImageUploadConfigTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadScreenshots",
                table: "ImageUploadConfigTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadCover",
                table: "ImageUploadConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadScreenshotGrid",
                table: "ImageUploadConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "UploadScreenshots",
                table: "ImageUploadConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploads_ReleaseScreenshotId",
                table: "ImageUploads",
                column: "ReleaseScreenshotId"
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

            migrationBuilder.DropIndex(
                name: "IX_ImageUploads_ReleaseScreenshotId",
                table: "ImageUploads"
            );

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
