using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUseReleaseCollectionsFromTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "UseReleaseCollections", table: "ReleaseTemplates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UseReleaseCollections",
                table: "ReleaseTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }
    }
}
