using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddArchiveFileSizeToArchives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArchiveFileSizeMb",
                table: "Archives",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.Sql(
                """
                UPDATE "Archives" AS a
                SET "ArchiveFileSizeMb" = ac."ArchiveFileSizeMb"
                FROM "ArchiveConfigs" AS ac
                WHERE a."ArchiveConfigId" = ac."Id"
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ArchiveFileSizeMb", table: "Archives");
        }
    }
}
