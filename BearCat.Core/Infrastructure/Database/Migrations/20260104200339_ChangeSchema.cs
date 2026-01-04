using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BearCat.Core.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DistributionUploads_Distributions_DistributionId",
                table: "DistributionUploads");

            migrationBuilder.DropIndex(
                name: "IX_DistributionUploads_DistributionId",
                table: "DistributionUploads");

            migrationBuilder.DropColumn(
                name: "DistributionId",
                table: "DistributionUploads");

            migrationBuilder.CreateTable(
                name: "DistributionArchive",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DistributionId = table.Column<int>(type: "integer", nullable: false),
                    ArchiveUploadId = table.Column<int>(type: "integer", nullable: true),
                    ArchiveFilePaths = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionArchive", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionArchive_DistributionUploads_ArchiveUploadId",
                        column: x => x.ArchiveUploadId,
                        principalTable: "DistributionUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DistributionArchive_Distributions_DistributionId",
                        column: x => x.DistributionId,
                        principalTable: "Distributions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistributionArchive_ArchiveUploadId",
                table: "DistributionArchive",
                column: "ArchiveUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionArchive_DistributionId",
                table: "DistributionArchive",
                column: "DistributionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistributionArchive");

            migrationBuilder.AddColumn<int>(
                name: "DistributionId",
                table: "DistributionUploads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_DistributionUploads_DistributionId",
                table: "DistributionUploads",
                column: "DistributionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DistributionUploads_Distributions_DistributionId",
                table: "DistributionUploads",
                column: "DistributionId",
                principalTable: "Distributions",
                principalColumn: "Id");
        }
    }
}
