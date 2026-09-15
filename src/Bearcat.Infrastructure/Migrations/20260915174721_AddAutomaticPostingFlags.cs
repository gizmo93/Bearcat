using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticPostingFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableAutomaticPosting",
                table: "DistributionSiteRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "StripDotsForThreadSearch",
                table: "DistributionSiteRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableAutomaticPosting",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "StripDotsForThreadSearch",
                table: "DistributionSiteRegistrations"
            );
        }
    }
}
