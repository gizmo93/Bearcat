using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddUniqueReleaseNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Releases"
                SET "Name" = substr("Releases"."Name", 1, 491) || ' ' || substr(md5(random()::text || "Releases"."Id"), 1, 8)
                FROM (
                    SELECT "Id", row_number() OVER (PARTITION BY lower("Name") ORDER BY "Id") AS "RowNumber"
                    FROM "Releases"
                ) AS "DuplicateReleases"
                WHERE "Releases"."Id" = "DuplicateReleases"."Id" AND "DuplicateReleases"."RowNumber" > 1
                """
            );

            migrationBuilder.Sql(
                """CREATE UNIQUE INDEX "IX_Releases_NameLowercase" ON "Releases" (lower("Name"))"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Releases_NameLowercase", table: "Releases");
        }
    }
}
