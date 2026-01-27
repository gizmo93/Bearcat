using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ChangeNotificationTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ArchiveNotification_Archives_ArchiveId",
            table: "ArchiveNotification");

        migrationBuilder.DropForeignKey(
            name: "FK_ArchiveNotification_Notifications_NotificationId",
            table: "ArchiveNotification");

        migrationBuilder.DropForeignKey(
            name: "FK_LinkCrypterContainerNotification_LinkCrypterContainers_Link~",
            table: "LinkCrypterContainerNotification");

        migrationBuilder.DropForeignKey(
            name: "FK_LinkCrypterContainerNotification_Notifications_Notification~",
            table: "LinkCrypterContainerNotification");

        migrationBuilder.DropForeignKey(
            name: "FK_UploadNotification_Notifications_NotificationId",
            table: "UploadNotification");

        migrationBuilder.DropForeignKey(
            name: "FK_UploadNotification_Uploads_UploadId",
            table: "UploadNotification");

        migrationBuilder.DropPrimaryKey(
            name: "PK_UploadNotification",
            table: "UploadNotification");

        migrationBuilder.DropPrimaryKey(
            name: "PK_LinkCrypterContainerNotification",
            table: "LinkCrypterContainerNotification");

        migrationBuilder.DropPrimaryKey(
            name: "PK_ArchiveNotification",
            table: "ArchiveNotification");

        migrationBuilder.RenameTable(
            name: "UploadNotification",
            newName: "UploadNotifications");

        migrationBuilder.RenameTable(
            name: "LinkCrypterContainerNotification",
            newName: "LinkCrypterContainerNotifications");

        migrationBuilder.RenameTable(
            name: "ArchiveNotification",
            newName: "ArchiveNotifications");

        migrationBuilder.RenameIndex(
            name: "IX_UploadNotification_UploadId",
            table: "UploadNotifications",
            newName: "IX_UploadNotifications_UploadId");

        migrationBuilder.RenameIndex(
            name: "IX_LinkCrypterContainerNotification_LinkCrypterContainerId",
            table: "LinkCrypterContainerNotifications",
            newName: "IX_LinkCrypterContainerNotifications_LinkCrypterContainerId");

        migrationBuilder.RenameIndex(
            name: "IX_ArchiveNotification_ArchiveId",
            table: "ArchiveNotifications",
            newName: "IX_ArchiveNotifications_ArchiveId");

        migrationBuilder.AddColumn<int>(
            name: "ArchiveNotificationNotificationId",
            table: "Notifications",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "LinkCrypterContainerNotificationNotificationId",
            table: "Notifications",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "UploadNotificationNotificationId",
            table: "Notifications",
            type: "integer",
            nullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "UploadId",
            table: "UploadNotifications",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AddColumn<int>(
            name: "EntityId",
            table: "UploadNotifications",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AlterColumn<int>(
            name: "LinkCrypterContainerId",
            table: "LinkCrypterContainerNotifications",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AddColumn<int>(
            name: "EntityId",
            table: "LinkCrypterContainerNotifications",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AlterColumn<int>(
            name: "ArchiveId",
            table: "ArchiveNotifications",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AddColumn<int>(
            name: "EntityId",
            table: "ArchiveNotifications",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddPrimaryKey(
            name: "PK_UploadNotifications",
            table: "UploadNotifications",
            column: "NotificationId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_LinkCrypterContainerNotifications",
            table: "LinkCrypterContainerNotifications",
            column: "NotificationId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_ArchiveNotifications",
            table: "ArchiveNotifications",
            column: "NotificationId");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_ArchiveNotificationNotificationId",
            table: "Notifications",
            column: "ArchiveNotificationNotificationId");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_LinkCrypterContainerNotificationNotificationId",
            table: "Notifications",
            column: "LinkCrypterContainerNotificationNotificationId");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_UploadNotificationNotificationId",
            table: "Notifications",
            column: "UploadNotificationNotificationId");

        migrationBuilder.CreateIndex(
            name: "IX_UploadNotifications_EntityId",
            table: "UploadNotifications",
            column: "EntityId");

        migrationBuilder.CreateIndex(
            name: "IX_LinkCrypterContainerNotifications_EntityId",
            table: "LinkCrypterContainerNotifications",
            column: "EntityId");

        migrationBuilder.CreateIndex(
            name: "IX_ArchiveNotifications_EntityId",
            table: "ArchiveNotifications",
            column: "EntityId");

        migrationBuilder.AddForeignKey(
            name: "FK_ArchiveNotifications_Archives_ArchiveId",
            table: "ArchiveNotifications",
            column: "ArchiveId",
            principalTable: "Archives",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_ArchiveNotifications_Archives_EntityId",
            table: "ArchiveNotifications",
            column: "EntityId",
            principalTable: "Archives",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_ArchiveNotifications_Notifications_NotificationId",
            table: "ArchiveNotifications",
            column: "NotificationId",
            principalTable: "Notifications",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_LinkCrypterContainerNotifications_LinkCrypterContainers_Ent~",
            table: "LinkCrypterContainerNotifications",
            column: "EntityId",
            principalTable: "LinkCrypterContainers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_LinkCrypterContainerNotifications_LinkCrypterContainers_Lin~",
            table: "LinkCrypterContainerNotifications",
            column: "LinkCrypterContainerId",
            principalTable: "LinkCrypterContainers",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_LinkCrypterContainerNotifications_Notifications_Notificatio~",
            table: "LinkCrypterContainerNotifications",
            column: "NotificationId",
            principalTable: "Notifications",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Notifications_ArchiveNotifications_ArchiveNotificationNotif~",
            table: "Notifications",
            column: "ArchiveNotificationNotificationId",
            principalTable: "ArchiveNotifications",
            principalColumn: "NotificationId");

        migrationBuilder.AddForeignKey(
            name: "FK_Notifications_LinkCrypterContainerNotifications_LinkCrypter~",
            table: "Notifications",
            column: "LinkCrypterContainerNotificationNotificationId",
            principalTable: "LinkCrypterContainerNotifications",
            principalColumn: "NotificationId");

        migrationBuilder.AddForeignKey(
            name: "FK_Notifications_UploadNotifications_UploadNotificationNotific~",
            table: "Notifications",
            column: "UploadNotificationNotificationId",
            principalTable: "UploadNotifications",
            principalColumn: "NotificationId");

        migrationBuilder.AddForeignKey(
            name: "FK_UploadNotifications_Notifications_NotificationId",
            table: "UploadNotifications",
            column: "NotificationId",
            principalTable: "Notifications",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_UploadNotifications_Uploads_EntityId",
            table: "UploadNotifications",
            column: "EntityId",
            principalTable: "Uploads",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_UploadNotifications_Uploads_UploadId",
            table: "UploadNotifications",
            column: "UploadId",
            principalTable: "Uploads",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ArchiveNotifications_Archives_ArchiveId",
            table: "ArchiveNotifications");

        migrationBuilder.DropForeignKey(
            name: "FK_ArchiveNotifications_Archives_EntityId",
            table: "ArchiveNotifications");

        migrationBuilder.DropForeignKey(
            name: "FK_ArchiveNotifications_Notifications_NotificationId",
            table: "ArchiveNotifications");

        migrationBuilder.DropForeignKey(
            name: "FK_LinkCrypterContainerNotifications_LinkCrypterContainers_Ent~",
            table: "LinkCrypterContainerNotifications");

        migrationBuilder.DropForeignKey(
            name: "FK_LinkCrypterContainerNotifications_LinkCrypterContainers_Lin~",
            table: "LinkCrypterContainerNotifications");

        migrationBuilder.DropForeignKey(
            name: "FK_LinkCrypterContainerNotifications_Notifications_Notificatio~",
            table: "LinkCrypterContainerNotifications");

        migrationBuilder.DropForeignKey(
            name: "FK_Notifications_ArchiveNotifications_ArchiveNotificationNotif~",
            table: "Notifications");

        migrationBuilder.DropForeignKey(
            name: "FK_Notifications_LinkCrypterContainerNotifications_LinkCrypter~",
            table: "Notifications");

        migrationBuilder.DropForeignKey(
            name: "FK_Notifications_UploadNotifications_UploadNotificationNotific~",
            table: "Notifications");

        migrationBuilder.DropForeignKey(
            name: "FK_UploadNotifications_Notifications_NotificationId",
            table: "UploadNotifications");

        migrationBuilder.DropForeignKey(
            name: "FK_UploadNotifications_Uploads_EntityId",
            table: "UploadNotifications");

        migrationBuilder.DropForeignKey(
            name: "FK_UploadNotifications_Uploads_UploadId",
            table: "UploadNotifications");

        migrationBuilder.DropIndex(
            name: "IX_Notifications_ArchiveNotificationNotificationId",
            table: "Notifications");

        migrationBuilder.DropIndex(
            name: "IX_Notifications_LinkCrypterContainerNotificationNotificationId",
            table: "Notifications");

        migrationBuilder.DropIndex(
            name: "IX_Notifications_UploadNotificationNotificationId",
            table: "Notifications");

        migrationBuilder.DropPrimaryKey(
            name: "PK_UploadNotifications",
            table: "UploadNotifications");

        migrationBuilder.DropIndex(
            name: "IX_UploadNotifications_EntityId",
            table: "UploadNotifications");

        migrationBuilder.DropPrimaryKey(
            name: "PK_LinkCrypterContainerNotifications",
            table: "LinkCrypterContainerNotifications");

        migrationBuilder.DropIndex(
            name: "IX_LinkCrypterContainerNotifications_EntityId",
            table: "LinkCrypterContainerNotifications");

        migrationBuilder.DropPrimaryKey(
            name: "PK_ArchiveNotifications",
            table: "ArchiveNotifications");

        migrationBuilder.DropIndex(
            name: "IX_ArchiveNotifications_EntityId",
            table: "ArchiveNotifications");

        migrationBuilder.DropColumn(
            name: "ArchiveNotificationNotificationId",
            table: "Notifications");

        migrationBuilder.DropColumn(
            name: "LinkCrypterContainerNotificationNotificationId",
            table: "Notifications");

        migrationBuilder.DropColumn(
            name: "UploadNotificationNotificationId",
            table: "Notifications");

        migrationBuilder.DropColumn(
            name: "EntityId",
            table: "UploadNotifications");

        migrationBuilder.DropColumn(
            name: "EntityId",
            table: "LinkCrypterContainerNotifications");

        migrationBuilder.DropColumn(
            name: "EntityId",
            table: "ArchiveNotifications");

        migrationBuilder.RenameTable(
            name: "UploadNotifications",
            newName: "UploadNotification");

        migrationBuilder.RenameTable(
            name: "LinkCrypterContainerNotifications",
            newName: "LinkCrypterContainerNotification");

        migrationBuilder.RenameTable(
            name: "ArchiveNotifications",
            newName: "ArchiveNotification");

        migrationBuilder.RenameIndex(
            name: "IX_UploadNotifications_UploadId",
            table: "UploadNotification",
            newName: "IX_UploadNotification_UploadId");

        migrationBuilder.RenameIndex(
            name: "IX_LinkCrypterContainerNotifications_LinkCrypterContainerId",
            table: "LinkCrypterContainerNotification",
            newName: "IX_LinkCrypterContainerNotification_LinkCrypterContainerId");

        migrationBuilder.RenameIndex(
            name: "IX_ArchiveNotifications_ArchiveId",
            table: "ArchiveNotification",
            newName: "IX_ArchiveNotification_ArchiveId");

        migrationBuilder.AlterColumn<int>(
            name: "UploadId",
            table: "UploadNotification",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "LinkCrypterContainerId",
            table: "LinkCrypterContainerNotification",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "ArchiveId",
            table: "ArchiveNotification",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.AddPrimaryKey(
            name: "PK_UploadNotification",
            table: "UploadNotification",
            column: "NotificationId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_LinkCrypterContainerNotification",
            table: "LinkCrypterContainerNotification",
            column: "NotificationId");

        migrationBuilder.AddPrimaryKey(
            name: "PK_ArchiveNotification",
            table: "ArchiveNotification",
            column: "NotificationId");

        migrationBuilder.AddForeignKey(
            name: "FK_ArchiveNotification_Archives_ArchiveId",
            table: "ArchiveNotification",
            column: "ArchiveId",
            principalTable: "Archives",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_ArchiveNotification_Notifications_NotificationId",
            table: "ArchiveNotification",
            column: "NotificationId",
            principalTable: "Notifications",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_LinkCrypterContainerNotification_LinkCrypterContainers_Link~",
            table: "LinkCrypterContainerNotification",
            column: "LinkCrypterContainerId",
            principalTable: "LinkCrypterContainers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_LinkCrypterContainerNotification_Notifications_Notification~",
            table: "LinkCrypterContainerNotification",
            column: "NotificationId",
            principalTable: "Notifications",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_UploadNotification_Notifications_NotificationId",
            table: "UploadNotification",
            column: "NotificationId",
            principalTable: "Notifications",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_UploadNotification_Uploads_UploadId",
            table: "UploadNotification",
            column: "UploadId",
            principalTable: "Uploads",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
