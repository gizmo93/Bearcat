using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BearCat.Core.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAdditionalEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Releases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReleaseType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Releases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Distributions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReleaseId = table.Column<int>(type: "integer", nullable: false),
                    HosterRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ArchiverFullClassName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ArchivePassword = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Distributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Distributions_HosterRegistrations_HosterRegistrationId",
                        column: x => x.HosterRegistrationId,
                        principalTable: "HosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Distributions_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DistributionUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DistributionId = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(4) with time zone", precision: 4, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp(4) with time zone", precision: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionUploads_Distributions_DistributionId",
                        column: x => x.DistributionId,
                        principalTable: "Distributions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HosterFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DistributionUploadId = table.Column<int>(type: "integer", nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HosterFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HosterFiles_DistributionUploads_DistributionUploadId",
                        column: x => x.DistributionUploadId,
                        principalTable: "DistributionUploads",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Distributions_HosterRegistrationId",
                table: "Distributions",
                column: "HosterRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_Distributions_ReleaseId",
                table: "Distributions",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionUploads_DistributionId",
                table: "DistributionUploads",
                column: "DistributionId");

            migrationBuilder.CreateIndex(
                name: "IX_HosterFiles_DistributionUploadId",
                table: "HosterFiles",
                column: "DistributionUploadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HosterFiles");

            migrationBuilder.DropTable(
                name: "DistributionUploads");

            migrationBuilder.DropTable(
                name: "Distributions");

            migrationBuilder.DropTable(
                name: "Releases");
        }
    }
}
