using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Core.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSchemaAgain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HosterFiles_DistributionUploads_DistributionUploadId",
                table: "HosterFiles");

            migrationBuilder.RenameColumn(
                name: "DistributionUploadId",
                table: "HosterFiles",
                newName: "ArchiveUploadId");

            migrationBuilder.RenameIndex(
                name: "IX_HosterFiles_DistributionUploadId",
                table: "HosterFiles",
                newName: "IX_HosterFiles_ArchiveUploadId");

            migrationBuilder.AddColumn<string>(
                name: "ArchiveNamePrefix",
                table: "Distributions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistributionFolderPath",
                table: "Distributions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TargetArchiveFileSizeMb",
                table: "Distributions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_HosterFiles_DistributionUploads_ArchiveUploadId",
                table: "HosterFiles",
                column: "ArchiveUploadId",
                principalTable: "DistributionUploads",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HosterFiles_DistributionUploads_ArchiveUploadId",
                table: "HosterFiles");

            migrationBuilder.DropColumn(
                name: "ArchiveNamePrefix",
                table: "Distributions");

            migrationBuilder.DropColumn(
                name: "DistributionFolderPath",
                table: "Distributions");

            migrationBuilder.DropColumn(
                name: "TargetArchiveFileSizeMb",
                table: "Distributions");

            migrationBuilder.RenameColumn(
                name: "ArchiveUploadId",
                table: "HosterFiles",
                newName: "DistributionUploadId");

            migrationBuilder.RenameIndex(
                name: "IX_HosterFiles_ArchiveUploadId",
                table: "HosterFiles",
                newName: "IX_HosterFiles_DistributionUploadId");

            migrationBuilder.AddForeignKey(
                name: "FK_HosterFiles_DistributionUploads_DistributionUploadId",
                table: "HosterFiles",
                column: "DistributionUploadId",
                principalTable: "DistributionUploads",
                principalColumn: "Id");
        }
    }
}
