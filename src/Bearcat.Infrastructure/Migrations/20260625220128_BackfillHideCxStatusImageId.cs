using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillHideCxStatusImageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "LinkCrypterContainers" AS c
                SET "StatusImageId" = c."ExternalReference"
                FROM "LinkCrypterRegistrations" AS r
                WHERE c."LinkCrypterRegistrationId" = r."Id"
                  AND r."LinkCrypterClassName" = 'HideCx'
                  AND c."StatusImageId" IS NULL
                  AND c."ExternalReference" IS NOT NULL;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "LinkCrypterContainers" AS c
                SET "StatusImageId" = NULL
                FROM "LinkCrypterRegistrations" AS r
                WHERE c."LinkCrypterRegistrationId" = r."Id"
                  AND r."LinkCrypterClassName" = 'HideCx'
                  AND c."StatusImageId" = c."ExternalReference";
                """
            );
        }
    }
}
