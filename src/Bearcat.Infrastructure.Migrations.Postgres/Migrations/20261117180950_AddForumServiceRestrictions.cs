using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddForumServiceRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "ForbiddenHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "ForbiddenImageHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "ForbiddenLinkCrypterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "RequiredHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "RequiredImageHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "RequiredLinkCrypterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForbiddenHosterClassNames",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "ForbiddenImageHosterClassNames",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "ForbiddenLinkCrypterClassNames",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "RequiredHosterClassNames",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "RequiredImageHosterClassNames",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "RequiredLinkCrypterClassNames",
                table: "DistributionSiteRegistrations"
            );
        }
    }
}
