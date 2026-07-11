using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrimaryLanguages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrimaryLanguageCode",
                table: "Releases",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "PrimaryLanguageCode",
                table: "ReleaseFolderAutomations",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "PrimaryLanguageCode",
                table: "ReleaseCollections",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PrimaryLanguageCode", table: "Releases");

            migrationBuilder.DropColumn(
                name: "PrimaryLanguageCode",
                table: "ReleaseFolderAutomations"
            );

            migrationBuilder.DropColumn(name: "PrimaryLanguageCode", table: "ReleaseCollections");
        }
    }
}
