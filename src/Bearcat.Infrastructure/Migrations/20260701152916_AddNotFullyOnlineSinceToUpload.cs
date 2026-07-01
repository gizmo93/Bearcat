using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotFullyOnlineSinceToUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NotFullyOnlineSince",
                table: "Uploads",
                type: "timestamp(4) without time zone",
                precision: 4,
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE "Uploads" AS u
                SET "NotFullyOnlineSince" = COALESCE(
                    (SELECT MIN(f."CheckedAt") FROM "UploadedFiles" AS f WHERE f."UploadId" = u."Id"),
                    u."CreatedAt"
                )
                WHERE u."OnlineState" IN (3, 4)
                  AND u."NotFullyOnlineSince" IS NULL;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NotFullyOnlineSince", table: "Uploads");
        }
    }
}
