using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixNotificationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArchiveId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkCrypterContainerId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UploadId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ArchiveId",
                table: "Notifications",
                column: "ArchiveId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_LinkCrypterContainerId",
                table: "Notifications",
                column: "LinkCrypterContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UploadId",
                table: "Notifications",
                column: "UploadId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Archives_ArchiveId",
                table: "Notifications",
                column: "ArchiveId",
                principalTable: "Archives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_LinkCrypterContainers_LinkCrypterContainerId",
                table: "Notifications",
                column: "LinkCrypterContainerId",
                principalTable: "LinkCrypterContainers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Uploads_UploadId",
                table: "Notifications",
                column: "UploadId",
                principalTable: "Uploads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Archives_ArchiveId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_LinkCrypterContainers_LinkCrypterContainerId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Uploads_UploadId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ArchiveId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_LinkCrypterContainerId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UploadId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ArchiveId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LinkCrypterContainerId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "UploadId",
                table: "Notifications");
        }
    }
}
