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
                name: "StripDotsForThreadSearch",
                table: "ForumPostingRules",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "EnableAutomaticPosting",
                table: "DistributionSiteRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripDotsForThreadSearch",
                table: "ForumPostingRules"
            );

            migrationBuilder.DropColumn(
                name: "EnableAutomaticPosting",
                table: "DistributionSiteRegistrations"
            );
        }
    }
}
