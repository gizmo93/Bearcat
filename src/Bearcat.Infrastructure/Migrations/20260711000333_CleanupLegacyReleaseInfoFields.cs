using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanupLegacyReleaseInfoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReleaseInfoId", table: "ReleaseNfos");

            migrationBuilder.DropColumn(name: "CoverUrl", table: "ReleaseInfos");

            migrationBuilder.DropColumn(name: "Description", table: "ReleaseInfos");

            migrationBuilder.DropColumn(name: "Genre", table: "ReleaseInfos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReleaseInfoId",
                table: "ReleaseNfos",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                table: "ReleaseInfos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ReleaseInfos",
                type: "text",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "Genre",
                table: "ReleaseInfos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE "ReleaseInfos" AS info
                SET
                    "Genre" = metadata."Genre",
                    "Description" = metadata."Description",
                    "CoverUrl" = metadata."CoverUrl"
                FROM "ReleaseMetadata" AS metadata
                WHERE metadata."ReleaseId" = info."ReleaseId";

                UPDATE "ReleaseNfos" AS nfo
                SET "ReleaseInfoId" = info."Id"
                FROM "ReleaseInfos" AS info
                WHERE info."ReleaseId" = nfo."ReleaseId";
                """
            );
        }
    }
}
