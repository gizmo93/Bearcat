using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillReleaseTemplateDetectionMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "ReleaseTemplates" SET "ReleaseCollectionDetectionMode" = 1 WHERE "ReleaseCollectionDetectionMode" = 0;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only fix; the invalid 0 values are not worth restoring.
        }
    }
}
