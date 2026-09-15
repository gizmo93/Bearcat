using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReleaseClassifications",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    Season = table.Column<int>(type: "integer", nullable: true),
                    Episode = table.Column<int>(type: "integer", nullable: true),
                    Resolution = table.Column<int>(type: "integer", nullable: false),
                    ResolutionSource = table.Column<int>(type: "integer", nullable: false),
                    PrimaryLanguage = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    LanguageSource = table.Column<int>(type: "integer", nullable: false),
                    IsMultiLanguage = table.Column<bool>(type: "boolean", nullable: false),
                    ParserVersion = table.Column<int>(type: "integer", nullable: false),
                    ClassifiedAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseClassifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseClassifications_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseClassifications_ReleaseId",
                table: "ReleaseClassifications",
                column: "ReleaseId",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReleaseClassifications");
        }
    }
}
