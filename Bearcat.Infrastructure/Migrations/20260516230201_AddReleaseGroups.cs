using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BearCat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReleaseGroups",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    Name = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    EnableAutomaticReuploads = table.Column<bool>(type: "boolean", nullable: false),
                    NumberOfHoursUntilReupload = table.Column<int>(
                        type: "integer",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseGroups", x => x.Id);
                }
            );

            migrationBuilder.InsertData(
                table: "ReleaseGroups",
                columns: new[]
                {
                    "Id",
                    "Name",
                    "EnableAutomaticReuploads",
                    "NumberOfHoursUntilReupload",
                },
                values: new object[] { 1, "Default", true, 0 }
            );

            migrationBuilder.Sql(
                "ALTER TABLE \"ReleaseGroups\" ALTER COLUMN \"Id\" RESTART WITH 2;"
            );

            migrationBuilder.AddColumn<int>(
                name: "ReleaseGroupId",
                table: "Releases",
                type: "integer",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ReleaseGroupId",
                table: "Releases",
                column: "ReleaseGroupId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Releases_ReleaseGroups_ReleaseGroupId",
                table: "Releases",
                column: "ReleaseGroupId",
                principalTable: "ReleaseGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Releases_ReleaseGroups_ReleaseGroupId",
                table: "Releases"
            );

            migrationBuilder.DropIndex(name: "IX_Releases_ReleaseGroupId", table: "Releases");

            migrationBuilder.DropColumn(name: "ReleaseGroupId", table: "Releases");

            migrationBuilder.DropTable(name: "ReleaseGroups");
        }
    }
}
