using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeReleaseInfoOneToOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReleaseInfos_ReleaseId_NfoDatabaseClassName",
                table: "ReleaseInfos"
            );

            migrationBuilder.RenameColumn(
                name: "ReleaseInfosCheckedAt",
                table: "Releases",
                newName: "ReleaseInfoCheckedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseInfos_ReleaseId",
                table: "ReleaseInfos",
                column: "ReleaseId",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_ReleaseInfos_ReleaseId", table: "ReleaseInfos");

            migrationBuilder.RenameColumn(
                name: "ReleaseInfoCheckedAt",
                table: "Releases",
                newName: "ReleaseInfosCheckedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseInfos_ReleaseId_NfoDatabaseClassName",
                table: "ReleaseInfos",
                columns: new[] { "ReleaseId", "NfoDatabaseClassName" },
                unique: true
            );
        }
    }
}
