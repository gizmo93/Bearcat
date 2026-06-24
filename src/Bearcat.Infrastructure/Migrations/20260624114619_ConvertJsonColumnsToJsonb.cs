using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertJsonColumnsToJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"ReleaseMediaFiles\" ALTER COLUMN \"MediaInfoJson\" TYPE jsonb USING \"MediaInfoJson\"::jsonb;"
            );

            migrationBuilder.Sql(
                "ALTER TABLE \"QualityCheckRules\" ALTER COLUMN \"ParametersJson\" TYPE jsonb USING \"ParametersJson\"::jsonb;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"ReleaseMediaFiles\" ALTER COLUMN \"MediaInfoJson\" TYPE text USING \"MediaInfoJson\"::text;"
            );

            migrationBuilder.Sql(
                "ALTER TABLE \"QualityCheckRules\" ALTER COLUMN \"ParametersJson\" TYPE text USING \"ParametersJson\"::text;"
            );
        }
    }
}
