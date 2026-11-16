using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddLinkCrypterImportContainerImportedReleaseCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImportedReleaseCollectionId",
                table: "LinkCrypterImportContainers",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterImportContainers_ImportedReleaseCollectionId",
                table: "LinkCrypterImportContainers",
                column: "ImportedReleaseCollectionId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LinkCrypterImportContainers_ReleaseCollections_ImportedReleaseCollectionId",
                table: "LinkCrypterImportContainers",
                column: "ImportedReleaseCollectionId",
                principalTable: "ReleaseCollections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LinkCrypterImportContainers_ReleaseCollections_ImportedReleaseCollectionId",
                table: "LinkCrypterImportContainers"
            );

            migrationBuilder.DropIndex(
                name: "IX_LinkCrypterImportContainers_ImportedReleaseCollectionId",
                table: "LinkCrypterImportContainers"
            );

            migrationBuilder.DropColumn(
                name: "ImportedReleaseCollectionId",
                table: "LinkCrypterImportContainers"
            );
        }
    }
}
