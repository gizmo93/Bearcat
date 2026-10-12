using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class LimitUploadsUploadedAtIndexToUploadedUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Uploads_UploadedAt", table: "Uploads");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UploadedAt",
                table: "Uploads",
                column: "UploadedAt",
                filter: "\"UploadedAt\" IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Uploads_UploadedAt", table: "Uploads");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UploadedAt",
                table: "Uploads",
                column: "UploadedAt"
            );
        }
    }
}
