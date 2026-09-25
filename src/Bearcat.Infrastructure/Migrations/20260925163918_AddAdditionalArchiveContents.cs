using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdditionalArchiveContents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "ReleaseFolderEntriesCopiedForPacking",
                table: "Archives",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.CreateTable(
                name: "AdditionalArchiveContents",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    Name = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    SourcePath = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: true
                    ),
                    FileName = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    TextContent = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdditionalArchiveContents", x => x.Id);
                    table.CheckConstraint(
                        "CK_AdditionalArchiveContent_FieldsMatchType",
                        "(\"Type\" = 1 AND \"SourcePath\" IS NOT NULL AND \"FileName\" IS NULL AND \"TextContent\" IS NULL) OR (\"Type\" = 2 AND \"SourcePath\" IS NULL AND \"FileName\" IS NOT NULL AND \"TextContent\" IS NOT NULL)"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ArchiveConfigAdditionalArchiveContents",
                columns: table => new
                {
                    ArchiveConfigId = table.Column<int>(type: "integer", nullable: false),
                    AdditionalArchiveContentId = table.Column<int>(
                        type: "integer",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_ArchiveConfigAdditionalArchiveContents",
                        x => new { x.ArchiveConfigId, x.AdditionalArchiveContentId }
                    );
                    table.ForeignKey(
                        name: "FK_ArchiveConfigAdditionalArchiveContents_AdditionalArchiveCon~",
                        column: x => x.AdditionalArchiveContentId,
                        principalTable: "AdditionalArchiveContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_ArchiveConfigAdditionalArchiveContents_ArchiveConfigs_Archi~",
                        column: x => x.ArchiveConfigId,
                        principalTable: "ArchiveConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ArchiveConfigTemplateAdditionalArchiveContents",
                columns: table => new
                {
                    ArchiveConfigTemplateId = table.Column<int>(type: "integer", nullable: false),
                    AdditionalArchiveContentId = table.Column<int>(
                        type: "integer",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_ArchiveConfigTemplateAdditionalArchiveContents",
                        x => new { x.ArchiveConfigTemplateId, x.AdditionalArchiveContentId }
                    );
                    table.ForeignKey(
                        name: "FK_ArchiveConfigTemplateAdditionalArchiveContents_AdditionalAr~",
                        column: x => x.AdditionalArchiveContentId,
                        principalTable: "AdditionalArchiveContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_ArchiveConfigTemplateAdditionalArchiveContents_ArchiveConfi~",
                        column: x => x.ArchiveConfigTemplateId,
                        principalTable: "ArchiveConfigTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_AdditionalArchiveContents_Name",
                table: "AdditionalArchiveContents",
                column: "Name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveConfigAdditionalArchiveContents_AdditionalArchiveCon~",
                table: "ArchiveConfigAdditionalArchiveContents",
                column: "AdditionalArchiveContentId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveConfigTemplateAdditionalArchiveContents_AdditionalAr~",
                table: "ArchiveConfigTemplateAdditionalArchiveContents",
                column: "AdditionalArchiveContentId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ArchiveConfigAdditionalArchiveContents");

            migrationBuilder.DropTable(name: "ArchiveConfigTemplateAdditionalArchiveContents");

            migrationBuilder.DropTable(name: "AdditionalArchiveContents");

            migrationBuilder.DropColumn(
                name: "ReleaseFolderEntriesCopiedForPacking",
                table: "Archives"
            );
        }
    }
}
