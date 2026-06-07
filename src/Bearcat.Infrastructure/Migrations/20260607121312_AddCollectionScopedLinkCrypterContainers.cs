using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionScopedLinkCrypterContainers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContainerScope",
                table: "UploadConfigLinkCrypterTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ContainerScope",
                table: "UploadConfigLinkCrypters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "UploadId",
                table: "LinkCrypterContainers",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "UploadConfigLinkCrypterId",
                table: "LinkCrypterContainers",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "CollectionUploadSlotId",
                table: "LinkCrypterContainers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkCrypterRegistrationId",
                table: "LinkCrypterContainers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "LinkCrypterContainers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "LinkCrypterContainers" AS container
                SET "LinkCrypterRegistrationId" = link_crypter."LinkCrypterRegistrationId"
                FROM "UploadConfigLinkCrypters" AS link_crypter
                WHERE container."UploadConfigLinkCrypterId" = link_crypter."Id";
                """
            );

            migrationBuilder.CreateTable(
                name: "LinkCrypterContainerSourceUploads",
                columns: table => new
                {
                    LinkCrypterContainerId = table.Column<int>(type: "integer", nullable: false),
                    UploadId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkCrypterContainerSourceUploads", x => new { x.LinkCrypterContainerId, x.UploadId });
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainerSourceUploads_LinkCrypterContainers_Lin~",
                        column: x => x.LinkCrypterContainerId,
                        principalTable: "LinkCrypterContainers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainerSourceUploads_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "LinkCrypterContainerSourceUploads" ("LinkCrypterContainerId", "UploadId")
                SELECT "Id", "UploadId"
                FROM "LinkCrypterContainers"
                WHERE "UploadId" IS NOT NULL
                ON CONFLICT DO NOTHING;
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainers_CollectionUploadSlotId",
                table: "LinkCrypterContainers",
                column: "CollectionUploadSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainers_LinkCrypterRegistrationId",
                table: "LinkCrypterContainers",
                column: "LinkCrypterRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainerSourceUploads_UploadId",
                table: "LinkCrypterContainerSourceUploads",
                column: "UploadId");

            migrationBuilder.AddForeignKey(
                name: "FK_LinkCrypterContainers_CollectionUploadSlots_CollectionUploa~",
                table: "LinkCrypterContainers",
                column: "CollectionUploadSlotId",
                principalTable: "CollectionUploadSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LinkCrypterContainers_LinkCrypterRegistrations_LinkCrypterR~",
                table: "LinkCrypterContainers",
                column: "LinkCrypterRegistrationId",
                principalTable: "LinkCrypterRegistrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LinkCrypterContainers_CollectionUploadSlots_CollectionUploa~",
                table: "LinkCrypterContainers");

            migrationBuilder.DropForeignKey(
                name: "FK_LinkCrypterContainers_LinkCrypterRegistrations_LinkCrypterR~",
                table: "LinkCrypterContainers");

            migrationBuilder.DropTable(
                name: "LinkCrypterContainerSourceUploads");

            migrationBuilder.Sql(
                """
                DELETE FROM "LinkCrypterContainers"
                WHERE "Scope" <> 0
                   OR "UploadId" IS NULL
                   OR "UploadConfigLinkCrypterId" IS NULL;
                """
            );

            migrationBuilder.DropIndex(
                name: "IX_LinkCrypterContainers_CollectionUploadSlotId",
                table: "LinkCrypterContainers");

            migrationBuilder.DropIndex(
                name: "IX_LinkCrypterContainers_LinkCrypterRegistrationId",
                table: "LinkCrypterContainers");

            migrationBuilder.DropColumn(
                name: "ContainerScope",
                table: "UploadConfigLinkCrypterTemplates");

            migrationBuilder.DropColumn(
                name: "ContainerScope",
                table: "UploadConfigLinkCrypters");

            migrationBuilder.DropColumn(
                name: "CollectionUploadSlotId",
                table: "LinkCrypterContainers");

            migrationBuilder.DropColumn(
                name: "LinkCrypterRegistrationId",
                table: "LinkCrypterContainers");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "LinkCrypterContainers");

            migrationBuilder.AlterColumn<int>(
                name: "UploadId",
                table: "LinkCrypterContainers",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UploadConfigLinkCrypterId",
                table: "LinkCrypterContainers",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
