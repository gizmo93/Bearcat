using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddLinkCrypterContainerUpdateFailedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdateFailedAt",
                table: "LinkCrypterContainers",
                type: "TEXT",
                precision: 4,
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE "LinkCrypterContainers"
                SET "State" = 3
                WHERE "State" = 2 AND "ContainerUrl" <> '';
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "LinkCrypterContainers"
                SET "State" = 2
                WHERE "State" = 3;
                """
            );

            migrationBuilder.DropColumn(name: "UpdateFailedAt", table: "LinkCrypterContainers");
        }
    }
}
