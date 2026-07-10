using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MetadataCheckedAt",
                table: "Releases",
                type: "timestamp(4) without time zone",
                precision: 4,
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "ReleaseMetadata",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseId = table.Column<int>(type: "integer", nullable: false),
                    MetadataDatabaseClassName = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Title = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    Genre = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CoverUrl = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: true
                    ),
                    MetadataDatabaseUrl = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseMetadata_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseMetadata_ReleaseId",
                table: "ReleaseMetadata",
                column: "ReleaseId",
                unique: true
            );

            migrationBuilder.Sql(
                """
                INSERT INTO "ReleaseMetadata" (
                    "ReleaseId",
                    "MetadataDatabaseClassName",
                    "Title",
                    "Genre",
                    "Description",
                    "CoverUrl",
                    "MetadataDatabaseUrl"
                )
                SELECT
                    info."ReleaseId",
                    info."NfoDatabaseClassName",
                    COALESCE(
                        (
                            SELECT external."Title"
                            FROM "ReleaseExternalInfos" AS external
                            WHERE external."ReleaseInfoId" = info."Id"
                              AND external."Title" IS NOT NULL
                              AND btrim(external."Title") <> ''
                            ORDER BY external."Id"
                            LIMIT 1
                        ),
                        info."ReleaseName"
                    ),
                    info."Genre",
                    info."Description",
                    info."CoverUrl",
                    NULL
                FROM "ReleaseInfos" AS info;

                UPDATE "Releases" AS release
                SET "MetadataCheckedAt" = release."ReleaseInfoCheckedAt"
                WHERE EXISTS (
                    SELECT 1
                    FROM "ReleaseMetadata" AS metadata
                    WHERE metadata."ReleaseId" = release."Id"
                );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "ReleaseInfos" (
                    "ReleaseId",
                    "NfoDatabaseClassName",
                    "ReleaseName"
                )
                SELECT
                    metadata."ReleaseId",
                    metadata."MetadataDatabaseClassName",
                    metadata."Title"
                FROM "ReleaseMetadata" AS metadata
                LEFT JOIN "ReleaseInfos" AS info ON info."ReleaseId" = metadata."ReleaseId"
                WHERE info."Id" IS NULL;

                UPDATE "ReleaseInfos" AS info
                SET
                    "Genre" = metadata."Genre",
                    "Description" = metadata."Description",
                    "CoverUrl" = metadata."CoverUrl"
                FROM "ReleaseMetadata" AS metadata
                WHERE metadata."ReleaseId" = info."ReleaseId";
                """
            );

            migrationBuilder.DropTable(name: "ReleaseMetadata");

            migrationBuilder.DropColumn(name: "MetadataCheckedAt", table: "Releases");
        }
    }
}
