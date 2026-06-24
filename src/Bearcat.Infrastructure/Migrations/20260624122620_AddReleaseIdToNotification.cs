using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseIdToNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReleaseId",
                table: "Notifications",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ReleaseId",
                table: "Notifications",
                column: "ReleaseId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Releases_ReleaseId",
                table: "Notifications",
                column: "ReleaseId",
                principalTable: "Releases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Releases_ReleaseId",
                table: "Notifications"
            );

            migrationBuilder.DropIndex(name: "IX_Notifications_ReleaseId", table: "Notifications");

            migrationBuilder.DropColumn(name: "ReleaseId", table: "Notifications");
        }
    }
}
