using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusImageIdToLinkCrypterContainer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatusImageId",
                table: "LinkCrypterContainers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "StatusImageId", table: "LinkCrypterContainers");
        }
    }
}
