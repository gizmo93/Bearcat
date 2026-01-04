using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Core.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RenameTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DistributionArchive_DistributionUploads_ArchiveUploadId",
                table: "DistributionArchive");

            migrationBuilder.DropForeignKey(
                name: "FK_DistributionArchive_Distributions_DistributionId",
                table: "DistributionArchive");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DistributionArchive",
                table: "DistributionArchive");

            migrationBuilder.RenameTable(
                name: "DistributionArchive",
                newName: "DistributionArchives");

            migrationBuilder.RenameIndex(
                name: "IX_DistributionArchive_DistributionId",
                table: "DistributionArchives",
                newName: "IX_DistributionArchives_DistributionId");

            migrationBuilder.RenameIndex(
                name: "IX_DistributionArchive_ArchiveUploadId",
                table: "DistributionArchives",
                newName: "IX_DistributionArchives_ArchiveUploadId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DistributionArchives",
                table: "DistributionArchives",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DistributionArchives_DistributionUploads_ArchiveUploadId",
                table: "DistributionArchives",
                column: "ArchiveUploadId",
                principalTable: "DistributionUploads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DistributionArchives_Distributions_DistributionId",
                table: "DistributionArchives",
                column: "DistributionId",
                principalTable: "Distributions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DistributionArchives_DistributionUploads_ArchiveUploadId",
                table: "DistributionArchives");

            migrationBuilder.DropForeignKey(
                name: "FK_DistributionArchives_Distributions_DistributionId",
                table: "DistributionArchives");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DistributionArchives",
                table: "DistributionArchives");

            migrationBuilder.RenameTable(
                name: "DistributionArchives",
                newName: "DistributionArchive");

            migrationBuilder.RenameIndex(
                name: "IX_DistributionArchives_DistributionId",
                table: "DistributionArchive",
                newName: "IX_DistributionArchive_DistributionId");

            migrationBuilder.RenameIndex(
                name: "IX_DistributionArchives_ArchiveUploadId",
                table: "DistributionArchive",
                newName: "IX_DistributionArchive_ArchiveUploadId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DistributionArchive",
                table: "DistributionArchive",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DistributionArchive_DistributionUploads_ArchiveUploadId",
                table: "DistributionArchive",
                column: "ArchiveUploadId",
                principalTable: "DistributionUploads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DistributionArchive_Distributions_DistributionId",
                table: "DistributionArchive",
                column: "DistributionId",
                principalTable: "Distributions",
                principalColumn: "Id");
        }
    }
}
