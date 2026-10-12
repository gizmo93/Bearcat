using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddQueryPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Uploads_UploadConfigId", table: "Uploads");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_CreatedAt_Id",
                table: "Uploads",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]
            );

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UploadConfigId_OnlineState",
                table: "Uploads",
                columns: new[] { "UploadConfigId", "OnlineState" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UploadConfigId_UploadState_UploadedAt",
                table: "Uploads",
                columns: new[] { "UploadConfigId", "UploadState", "UploadedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UploadedAt",
                table: "Uploads",
                column: "UploadedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Releases_CreatedAt_Id",
                table: "Releases",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]
            );

            migrationBuilder.CreateIndex(
                name: "IX_Releases_Name_Id",
                table: "Releases",
                columns: new[] { "Name", "Id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Archives_CreatedAt_Id",
                table: "Archives",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Uploads_CreatedAt_Id", table: "Uploads");

            migrationBuilder.DropIndex(
                name: "IX_Uploads_UploadConfigId_OnlineState",
                table: "Uploads"
            );

            migrationBuilder.DropIndex(
                name: "IX_Uploads_UploadConfigId_UploadState_UploadedAt",
                table: "Uploads"
            );

            migrationBuilder.DropIndex(name: "IX_Uploads_UploadedAt", table: "Uploads");

            migrationBuilder.DropIndex(name: "IX_Releases_CreatedAt_Id", table: "Releases");

            migrationBuilder.DropIndex(name: "IX_Releases_Name_Id", table: "Releases");

            migrationBuilder.DropIndex(name: "IX_Archives_CreatedAt_Id", table: "Archives");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UploadConfigId",
                table: "Uploads",
                column: "UploadConfigId"
            );
        }
    }
}
