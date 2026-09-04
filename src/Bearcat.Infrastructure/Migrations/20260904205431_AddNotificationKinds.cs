using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NotificationType",
                table: "Notifications",
                newName: "NotificationSeverity"
            );

            migrationBuilder.AddColumn<int>(
                name: "NotificationKind",
                table: "Notifications",
                type: "integer",
                nullable: false,
                defaultValue: 1
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NotificationKind", table: "Notifications");

            migrationBuilder.RenameColumn(
                name: "NotificationSeverity",
                table: "Notifications",
                newName: "NotificationType"
            );
        }
    }
}
