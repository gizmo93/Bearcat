using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class MakeReleaseCollectionKeyUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReleaseCollections_ReleaseGroupId_Key",
                table: "ReleaseCollections"
            );

            migrationBuilder.Sql(
                """
                UPDATE "ReleaseCollections"
                SET "Key" = substr("ReleaseCollections"."Key", 1, 491) || '.' || lower(hex(randomblob(4)))
                FROM (
                    SELECT "Id", row_number() OVER (PARTITION BY "Key" ORDER BY "Id") AS "RowNumber"
                    FROM "ReleaseCollections"
                ) AS "DuplicateReleaseCollections"
                WHERE "ReleaseCollections"."Id" = "DuplicateReleaseCollections"."Id" AND "DuplicateReleaseCollections"."RowNumber" > 1
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCollections_Key",
                table: "ReleaseCollections",
                column: "Key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCollections_ReleaseGroupId",
                table: "ReleaseCollections",
                column: "ReleaseGroupId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReleaseCollections_Key",
                table: "ReleaseCollections"
            );

            migrationBuilder.DropIndex(
                name: "IX_ReleaseCollections_ReleaseGroupId",
                table: "ReleaseCollections"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCollections_ReleaseGroupId_Key",
                table: "ReleaseCollections",
                columns: new[] { "ReleaseGroupId", "Key" },
                unique: true
            );
        }
    }
}
