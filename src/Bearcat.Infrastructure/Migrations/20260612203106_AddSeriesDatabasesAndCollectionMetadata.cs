using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesDatabasesAndCollectionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MetadataCheckedAt",
                table: "ReleaseCollections",
                type: "timestamp without time zone",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "ReleaseCollectionMetadata",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseCollectionId = table.Column<int>(type: "integer", nullable: false),
                    SeriesDatabaseClassName = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Title = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CoverUrl = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: true
                    ),
                    SeriesDatabaseUrl = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseCollectionMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseCollectionMetadata_ReleaseCollections_ReleaseCollect~",
                        column: x => x.ReleaseCollectionId,
                        principalTable: "ReleaseCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "SeriesDatabaseRegistrations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    SeriesDatabaseClassName = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    SerializedConfig = table.Column<string>(
                        type: "character varying(4000)",
                        maxLength: 4000,
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesDatabaseRegistrations", x => x.Id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCollectionMetadata_ReleaseCollectionId",
                table: "ReleaseCollectionMetadata",
                column: "ReleaseCollectionId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_SeriesDatabaseRegistrations_SeriesDatabaseClassName",
                table: "SeriesDatabaseRegistrations",
                column: "SeriesDatabaseClassName",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReleaseCollectionMetadata");

            migrationBuilder.DropTable(name: "SeriesDatabaseRegistrations");

            migrationBuilder.DropColumn(name: "MetadataCheckedAt", table: "ReleaseCollections");
        }
    }
}
