using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostedLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PostedLocations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseId = table.Column<int>(type: "integer", nullable: true),
                    ReleaseCollectionId = table.Column<int>(type: "integer", nullable: true),
                    Url = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: false
                    ),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostedLocations", x => x.Id);
                    table.CheckConstraint(
                        "CK_PostedLocation_Owner",
                        "(\"ReleaseId\" IS NOT NULL) <> (\"ReleaseCollectionId\" IS NOT NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_PostedLocations_ReleaseCollections_ReleaseCollectionId",
                        column: x => x.ReleaseCollectionId,
                        principalTable: "ReleaseCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_PostedLocations_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_PostedLocations_ReleaseCollectionId",
                table: "PostedLocations",
                column: "ReleaseCollectionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PostedLocations_ReleaseId",
                table: "PostedLocations",
                column: "ReleaseId"
            );

            migrationBuilder.Sql(
                """
                INSERT INTO "PostedLocations" ("ReleaseId", "Url", "CreatedAt")
                SELECT DISTINCT uc."ReleaseId", btrim(link), LOCALTIMESTAMP
                FROM "UploadConfigs" uc
                CROSS JOIN LATERAL unnest(uc."LinksDistributedTo") AS link
                WHERE btrim(link) <> '';
                """
            );

            migrationBuilder.DropColumn(name: "LinksDistributedTo", table: "UploadConfigTemplates");

            migrationBuilder.DropColumn(name: "LinksDistributedTo", table: "UploadConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PostedLocations");

            migrationBuilder.AddColumn<List<string>>(
                name: "LinksDistributedTo",
                table: "UploadConfigTemplates",
                type: "text[]",
                nullable: false
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "LinksDistributedTo",
                table: "UploadConfigs",
                type: "text[]",
                nullable: false
            );
        }
    }
}
