using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddReleaseOnlineStateAndUploadOldestUploadedFileCheckedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OldestUploadedFileCheckedAt",
                table: "Uploads",
                type: "TEXT",
                precision: 4,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "OnlineState",
                table: "Releases",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.Sql(
                """
                UPDATE "Releases" AS "Release"
                SET "OnlineState" = CASE
                    WHEN "UploadConfigCounts"."OnlineUploadConfigCount" = "UploadConfigCounts"."UploadConfigCount" THEN 2
                    WHEN "UploadConfigCounts"."OnlineUploadConfigCount" > 0 THEN 3
                    ELSE 4
                END
                FROM (
                    SELECT
                        "UploadConfig"."ReleaseId",
                        COUNT(*) AS "UploadConfigCount",
                        SUM(CASE WHEN EXISTS (
                            SELECT 1
                            FROM "Uploads" AS "Upload"
                            WHERE "Upload"."UploadConfigId" = "UploadConfig"."Id" AND "Upload"."OnlineState" = 2
                        ) THEN 1 ELSE 0 END) AS "OnlineUploadConfigCount"
                    FROM "UploadConfigs" AS "UploadConfig"
                    GROUP BY "UploadConfig"."ReleaseId"
                ) AS "UploadConfigCounts"
                WHERE "UploadConfigCounts"."ReleaseId" = "Release"."Id";
                """
            );

            migrationBuilder.Sql(
                """
                UPDATE "Uploads" AS "Upload"
                SET "OldestUploadedFileCheckedAt" = "OldestUploadedFileChecks"."OldestUploadedFileCheckedAt"
                FROM (
                    SELECT
                        "UploadedFile"."UploadId",
                        MIN(COALESCE("UploadedFile"."CheckedAt", '0001-01-01 00:00:00')) AS "OldestUploadedFileCheckedAt"
                    FROM "UploadedFiles" AS "UploadedFile"
                    GROUP BY "UploadedFile"."UploadId"
                ) AS "OldestUploadedFileChecks"
                WHERE "OldestUploadedFileChecks"."UploadId" = "Upload"."Id";
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_Releases_OnlineState",
                table: "Releases",
                column: "OnlineState"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Releases_OnlineState", table: "Releases");

            migrationBuilder.DropColumn(name: "OldestUploadedFileCheckedAt", table: "Uploads");

            migrationBuilder.DropColumn(name: "OnlineState", table: "Releases");
        }
    }
}
