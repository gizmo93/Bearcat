using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationHosterRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HosterRegistrationId",
                table: "Notifications",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_HosterRegistrationId",
                table: "Notifications",
                column: "HosterRegistrationId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_HosterRegistrations_HosterRegistrationId",
                table: "Notifications",
                column: "HosterRegistrationId",
                principalTable: "HosterRegistrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_HosterRegistrations_HosterRegistrationId",
                table: "Notifications"
            );

            migrationBuilder.DropIndex(
                name: "IX_Notifications_HosterRegistrationId",
                table: "Notifications"
            );

            migrationBuilder.DropColumn(name: "HosterRegistrationId", table: "Notifications");
        }
    }
}
