using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameSeriesDatabasesToMediaDatabases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "SeriesDatabaseRegistrations",
                newName: "MediaDatabaseRegistrations"
            );

            migrationBuilder.RenameColumn(
                name: "SeriesDatabaseClassName",
                table: "MediaDatabaseRegistrations",
                newName: "MediaDatabaseClassName"
            );

            migrationBuilder.RenameIndex(
                name: "IX_SeriesDatabaseRegistrations_SeriesDatabaseClassName",
                table: "MediaDatabaseRegistrations",
                newName: "IX_MediaDatabaseRegistrations_MediaDatabaseClassName"
            );

            migrationBuilder.Sql(
                """
                ALTER TABLE "MediaDatabaseRegistrations"
                RENAME CONSTRAINT "PK_SeriesDatabaseRegistrations" TO "PK_MediaDatabaseRegistrations";
                """
            );

            migrationBuilder.RenameColumn(
                name: "SeriesDatabaseUrl",
                table: "ReleaseCollectionMetadata",
                newName: "MetadataDatabaseUrl"
            );

            migrationBuilder.RenameColumn(
                name: "SeriesDatabaseClassName",
                table: "ReleaseCollectionMetadata",
                newName: "MetadataDatabaseClassName"
            );

            migrationBuilder.Sql(
                """
                UPDATE "MediaDatabaseRegistrations"
                SET "MediaDatabaseClassName" = 'TvdbMetadataDatabase'
                WHERE "MediaDatabaseClassName" = 'TvdbSeriesDatabase';

                UPDATE "ReleaseCollectionMetadata"
                SET "MetadataDatabaseClassName" = 'TvdbMetadataDatabase'
                WHERE "MetadataDatabaseClassName" = 'TvdbSeriesDatabase';

                UPDATE "ReleaseMetadata"
                SET "MetadataDatabaseClassName" = 'TvdbMetadataDatabase'
                WHERE "MetadataDatabaseClassName" = 'TvdbSeriesDatabase';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "MediaDatabaseRegistrations"
                SET "MediaDatabaseClassName" = 'TvdbSeriesDatabase'
                WHERE "MediaDatabaseClassName" = 'TvdbMetadataDatabase';

                UPDATE "ReleaseCollectionMetadata"
                SET "MetadataDatabaseClassName" = 'TvdbSeriesDatabase'
                WHERE "MetadataDatabaseClassName" = 'TvdbMetadataDatabase';

                UPDATE "ReleaseMetadata"
                SET "MetadataDatabaseClassName" = 'TvdbSeriesDatabase'
                WHERE "MetadataDatabaseClassName" = 'TvdbMetadataDatabase';
                """
            );

            migrationBuilder.RenameColumn(
                name: "MetadataDatabaseUrl",
                table: "ReleaseCollectionMetadata",
                newName: "SeriesDatabaseUrl"
            );

            migrationBuilder.RenameColumn(
                name: "MetadataDatabaseClassName",
                table: "ReleaseCollectionMetadata",
                newName: "SeriesDatabaseClassName"
            );

            migrationBuilder.Sql(
                """
                ALTER TABLE "MediaDatabaseRegistrations"
                RENAME CONSTRAINT "PK_MediaDatabaseRegistrations" TO "PK_SeriesDatabaseRegistrations";
                """
            );

            migrationBuilder.RenameIndex(
                name: "IX_MediaDatabaseRegistrations_MediaDatabaseClassName",
                table: "MediaDatabaseRegistrations",
                newName: "IX_SeriesDatabaseRegistrations_SeriesDatabaseClassName"
            );

            migrationBuilder.RenameColumn(
                name: "MediaDatabaseClassName",
                table: "MediaDatabaseRegistrations",
                newName: "SeriesDatabaseClassName"
            );

            migrationBuilder.RenameTable(
                name: "MediaDatabaseRegistrations",
                newName: "SeriesDatabaseRegistrations"
            );
        }
    }
}
