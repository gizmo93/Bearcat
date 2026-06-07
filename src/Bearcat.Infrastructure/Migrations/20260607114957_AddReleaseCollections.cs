using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReleaseCollectionId",
                table: "Releases",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReleaseCollections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReleaseGroupId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp(4) without time zone", precision: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseCollections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseCollections_ReleaseGroups_ReleaseGroupId",
                        column: x => x.ReleaseGroupId,
                        principalTable: "ReleaseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ReleaseCollectionId",
                table: "Releases",
                column: "ReleaseCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCollections_ReleaseGroupId_Key",
                table: "ReleaseCollections",
                columns: new[] { "ReleaseGroupId", "Key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Releases_ReleaseCollections_ReleaseCollectionId",
                table: "Releases",
                column: "ReleaseCollectionId",
                principalTable: "ReleaseCollections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Releases_ReleaseCollections_ReleaseCollectionId",
                table: "Releases");

            migrationBuilder.DropTable(
                name: "ReleaseCollections");

            migrationBuilder.DropIndex(
                name: "IX_Releases_ReleaseCollectionId",
                table: "Releases");

            migrationBuilder.DropColumn(
                name: "ReleaseCollectionId",
                table: "Releases");
        }
    }
}
