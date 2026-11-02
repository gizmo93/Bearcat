using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddLinkCrypterContainerReleaseAllHostersScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReleaseId",
                table: "LinkCrypterContainers",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainers_ReleaseId_LinkCrypterRegistrationId",
                table: "LinkCrypterContainers",
                columns: new[] { "ReleaseId", "LinkCrypterRegistrationId" }
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LinkCrypterContainers_Releases_ReleaseId",
                table: "LinkCrypterContainers",
                column: "ReleaseId",
                principalTable: "Releases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LinkCrypterContainers_Releases_ReleaseId",
                table: "LinkCrypterContainers"
            );

            migrationBuilder.DropIndex(
                name: "IX_LinkCrypterContainers_ReleaseId_LinkCrypterRegistrationId",
                table: "LinkCrypterContainers"
            );

            migrationBuilder.DropColumn(name: "ReleaseId", table: "LinkCrypterContainers");
        }
    }
}
