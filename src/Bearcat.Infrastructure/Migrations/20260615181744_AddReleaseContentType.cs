using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseContentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReleaseContentType",
                table: "ReleaseTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "ReleaseContentType",
                table: "Releases",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "ReleaseContentType",
                table: "ReleaseCollections",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            // Releases that belong to a collection are treated as series episodes,
            // standalone releases default to Movie.
            migrationBuilder.Sql(
                """
                UPDATE "Releases"
                SET "ReleaseContentType" = CASE
                    WHEN "ReleaseCollectionId" IS NOT NULL THEN 2
                    ELSE 1
                END;
                """
            );

            // Collections are currently assumed to be series, so treat them as
            // TV show episodes (2).
            migrationBuilder.Sql(
                """
                UPDATE "ReleaseCollections" SET "ReleaseContentType" = 2;
                """
            );

            // Templates with release collection detection enabled (mode <> Disabled = 1)
            // produce series episodes, everything else defaults to Movie.
            migrationBuilder.Sql(
                """
                UPDATE "ReleaseTemplates"
                SET "ReleaseContentType" = CASE
                    WHEN "ReleaseCollectionDetectionMode" <> 1 THEN 2
                    ELSE 1
                END;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReleaseContentType", table: "ReleaseTemplates");

            migrationBuilder.DropColumn(name: "ReleaseContentType", table: "Releases");

            migrationBuilder.DropColumn(name: "ReleaseContentType", table: "ReleaseCollections");
        }
    }
}
