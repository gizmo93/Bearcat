using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UploadState_OnlineState",
                table: "Uploads",
                columns: new[] { "UploadState", "OnlineState" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ReleaseFolderPath",
                table: "Releases",
                column: "ReleaseFolderPath"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt_Id",
                table: "Notifications",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0],
                filter: "\"ResolvedAt\" IS NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Uploads_UploadState_OnlineState",
                table: "Uploads"
            );

            migrationBuilder.DropIndex(name: "IX_Releases_ReleaseFolderPath", table: "Releases");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CreatedAt_Id",
                table: "Notifications"
            );
        }
    }
}
