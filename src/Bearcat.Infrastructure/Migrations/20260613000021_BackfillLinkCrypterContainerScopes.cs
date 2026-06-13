using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLinkCrypterContainerScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddCollectionScopedLinkCrypterContainers added these enum columns with
            // database default 0, but the enum starts at Release = 1. Rows existing
            // before that migration therefore hold the invalid value 0 and are skipped
            // by every scope filter. Collection scope (2) is always set explicitly,
            // so every remaining 0 is a legacy release-scoped row.
            migrationBuilder.Sql(
                """
                UPDATE "LinkCrypterContainers" SET "Scope" = 1 WHERE "Scope" = 0;
                UPDATE "UploadConfigLinkCrypters" SET "ContainerScope" = 1 WHERE "ContainerScope" = 0;
                UPDATE "UploadConfigLinkCrypterTemplates" SET "ContainerScope" = 1 WHERE "ContainerScope" = 0;
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
