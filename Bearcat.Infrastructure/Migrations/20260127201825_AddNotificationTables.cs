using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveNotification",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "integer", nullable: false),
                    ArchiveId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveNotification", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_ArchiveNotification_Archives_ArchiveId",
                        column: x => x.ArchiveId,
                        principalTable: "Archives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArchiveNotification_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinkCrypterContainerNotification",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "integer", nullable: false),
                    LinkCrypterContainerId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkCrypterContainerNotification", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainerNotification_LinkCrypterContainers_Link~",
                        column: x => x.LinkCrypterContainerId,
                        principalTable: "LinkCrypterContainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainerNotification_Notifications_Notification~",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UploadNotification",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "integer", nullable: false),
                    UploadId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadNotification", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_UploadNotification_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UploadNotification_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveNotification_ArchiveId",
                table: "ArchiveNotification",
                column: "ArchiveId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainerNotification_LinkCrypterContainerId",
                table: "LinkCrypterContainerNotification",
                column: "LinkCrypterContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadNotification_UploadId",
                table: "UploadNotification",
                column: "UploadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveNotification");

            migrationBuilder.DropTable(
                name: "LinkCrypterContainerNotification");

            migrationBuilder.DropTable(
                name: "UploadNotification");
        }
    }
}
