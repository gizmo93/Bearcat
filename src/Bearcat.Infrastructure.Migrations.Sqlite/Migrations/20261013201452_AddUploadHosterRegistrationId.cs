using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddUploadHosterRegistrationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HosterRegistrationId",
                table: "Uploads",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.Sql(
                """
                UPDATE "Uploads"
                SET "HosterRegistrationId" = (
                    SELECT uc."HosterRegistrationId"
                    FROM "UploadConfigs" uc
                    WHERE uc."Id" = "Uploads"."UploadConfigId"
                );
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_HosterRegistrationId_OnlineState_OldestUploadedFileCheckedAt",
                table: "Uploads",
                columns: new[]
                {
                    "HosterRegistrationId",
                    "OnlineState",
                    "OldestUploadedFileCheckedAt",
                }
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Uploads_HosterRegistrations_HosterRegistrationId",
                table: "Uploads",
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
                name: "FK_Uploads_HosterRegistrations_HosterRegistrationId",
                table: "Uploads"
            );

            migrationBuilder.DropIndex(
                name: "IX_Uploads_HosterRegistrationId_OnlineState_OldestUploadedFileCheckedAt",
                table: "Uploads"
            );

            migrationBuilder.DropColumn(name: "HosterRegistrationId", table: "Uploads");
        }
    }
}
