using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseMediaFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MediaMetadataExtractedAt",
                table: "Releases",
                type: "timestamp(4) without time zone",
                precision: 4,
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "ReleaseMediaFiles",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseId = table.Column<int>(type: "integer", nullable: false),
                    RelativePath = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: false
                    ),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    MediaInfoJson = table.Column<string>(type: "text", nullable: false),
                    MediaInfoText = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseMediaFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseMediaFiles_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseMediaFiles_ReleaseId",
                table: "ReleaseMediaFiles",
                column: "ReleaseId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReleaseMediaFiles");

            migrationBuilder.DropColumn(name: "MediaMetadataExtractedAt", table: "Releases");
        }
    }
}
