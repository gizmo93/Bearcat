using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ReplaceAutoDeleteArchivesWithArchiveRetentionAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE ApplicationConfigurationOverrides SET PropertyName = 'ArchiveRetentionAction', SerializedValue = '2' WHERE ConfigurationKey = 'ArchiveCleanup' AND PropertyName = 'AutoDeleteArchives' AND SerializedValue = 'true';"
            );
            migrationBuilder.Sql(
                "DELETE FROM ApplicationConfigurationOverrides WHERE ConfigurationKey = 'ArchiveCleanup' AND PropertyName = 'AutoDeleteArchives';"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM ApplicationConfigurationOverrides WHERE ConfigurationKey = 'ArchiveCleanup' AND PropertyName = 'ArchiveRetentionAction' AND SerializedValue <> '2';"
            );
            migrationBuilder.Sql(
                "UPDATE ApplicationConfigurationOverrides SET PropertyName = 'AutoDeleteArchives', SerializedValue = 'true' WHERE ConfigurationKey = 'ArchiveCleanup' AND PropertyName = 'ArchiveRetentionAction';"
            );
        }
    }
}
