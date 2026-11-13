using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddReleaseCollectionOnlineStateAndUniqueNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OnlineState",
                table: "ReleaseCollections",
                type: "integer",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.Sql(
                """
                UPDATE "ReleaseCollections" AS "ReleaseCollection"
                SET "OnlineState" = CASE
                    WHEN "ReleaseCounts"."OnlineReleaseCount" = "ReleaseCounts"."KnownReleaseCount" THEN 2
                    WHEN "ReleaseCounts"."OnlineOrPartiallyOnlineReleaseCount" > 0 THEN 3
                    ELSE 4
                END
                FROM (
                    SELECT
                        "Release"."ReleaseCollectionId",
                        COUNT(*) AS "KnownReleaseCount",
                        SUM(CASE WHEN "Release"."OnlineState" = 2 THEN 1 ELSE 0 END) AS "OnlineReleaseCount",
                        SUM(CASE WHEN "Release"."OnlineState" IN (2, 3) THEN 1 ELSE 0 END) AS "OnlineOrPartiallyOnlineReleaseCount"
                    FROM "Releases" AS "Release"
                    WHERE "Release"."ReleaseCollectionId" IS NOT NULL AND "Release"."OnlineState" <> 1
                    GROUP BY "Release"."ReleaseCollectionId"
                ) AS "ReleaseCounts"
                WHERE "ReleaseCounts"."ReleaseCollectionId" = "ReleaseCollection"."Id";
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE "ReleaseCollections"
                SET "Name" = substr("ReleaseCollections"."Name", 1, 491) || ' ' || substr(md5(random()::text || "ReleaseCollections"."Id"), 1, 8)
                FROM (
                    SELECT "Id", row_number() OVER (PARTITION BY lower("Name") ORDER BY "Id") AS "RowNumber"
                    FROM "ReleaseCollections"
                ) AS "DuplicateReleaseCollections"
                WHERE "ReleaseCollections"."Id" = "DuplicateReleaseCollections"."Id" AND "DuplicateReleaseCollections"."RowNumber" > 1
                """
            );

            migrationBuilder.Sql(
                """CREATE UNIQUE INDEX "IX_ReleaseCollections_NameLowercase" ON "ReleaseCollections" (lower("Name"))"""
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCollections_CreatedAt_Id",
                table: "ReleaseCollections",
                columns: new[] { "CreatedAt", "Id" },
                descending: new bool[0]
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCollections_Name_Id",
                table: "ReleaseCollections",
                columns: new[] { "Name", "Id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCollections_OnlineState",
                table: "ReleaseCollections",
                column: "OnlineState"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReleaseCollections_NameLowercase",
                table: "ReleaseCollections"
            );

            migrationBuilder.DropIndex(
                name: "IX_ReleaseCollections_CreatedAt_Id",
                table: "ReleaseCollections"
            );

            migrationBuilder.DropIndex(
                name: "IX_ReleaseCollections_Name_Id",
                table: "ReleaseCollections"
            );

            migrationBuilder.DropIndex(
                name: "IX_ReleaseCollections_OnlineState",
                table: "ReleaseCollections"
            );

            migrationBuilder.DropColumn(name: "OnlineState", table: "ReleaseCollections");
        }
    }
}
