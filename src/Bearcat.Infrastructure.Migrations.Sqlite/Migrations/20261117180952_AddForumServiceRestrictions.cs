using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddForumServiceRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ForbiddenHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<string>(
                name: "ForbiddenImageHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<string>(
                name: "ForbiddenLinkCrypterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<string>(
                name: "RequiredHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<string>(
                name: "RequiredImageHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<string>(
                name: "RequiredLinkCrypterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
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
