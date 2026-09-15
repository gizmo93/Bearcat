using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDistributionSiteRegistrationToPostedLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DistributionSiteRegistrationId",
                table: "PostedLocations",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_PostedLocations_DistributionSiteRegistrationId",
                table: "PostedLocations",
                column: "DistributionSiteRegistrationId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_PostedLocations_DistributionSiteRegistrations_DistributionS~",
                table: "PostedLocations",
                column: "DistributionSiteRegistrationId",
                principalTable: "DistributionSiteRegistrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );

            migrationBuilder.Sql(
                """
                UPDATE "PostedLocations" AS pl
                SET "DistributionSiteRegistrationId" = r."Id"
                FROM "DistributionSiteRegistrations" AS r
                WHERE pl."DistributionSiteRegistrationId" IS NULL
                  AND (
                    (r."DistributionSiteClassName" = 'BoerseCx' AND pl."Url" ILIKE '%boerse.cx/%')
                    OR (r."DistributionSiteClassName" = 'DataLoadMe' AND pl."Url" ILIKE '%data-load.me/%')
                  );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PostedLocations_DistributionSiteRegistrations_DistributionS~",
                table: "PostedLocations"
            );

            migrationBuilder.DropIndex(
                name: "IX_PostedLocations_DistributionSiteRegistrationId",
                table: "PostedLocations"
            );

            migrationBuilder.DropColumn(
                name: "DistributionSiteRegistrationId",
                table: "PostedLocations"
            );
        }
    }
}
