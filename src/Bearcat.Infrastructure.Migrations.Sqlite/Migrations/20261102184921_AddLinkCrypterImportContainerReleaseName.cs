using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddLinkCrypterImportContainerReleaseName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReleaseName",
                table: "LinkCrypterImportContainers",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.Sql(
                """UPDATE "LinkCrypterImportContainers" SET "ReleaseName" = trim("ContainerName")"""
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ReleaseName", table: "LinkCrypterImportContainers");
        }
    }
}
