using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddLinkCrypterImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LinkCrypterImports",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    LinkCrypterRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    ReleaseTemplateId = table.Column<int>(type: "integer", nullable: false),
                    ArchiveFilesBasePathForReleasesWithoutLocalFiles = table.Column<string>(
                        type: "character varying(300)",
                        maxLength: 300,
                        nullable: true
                    ),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: false
                    ),
                    UpdatedAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: false
                    ),
                    ProcessedContainerCount = table.Column<int>(type: "integer", nullable: false),
                    ContainerCountToProcess = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessages = table.Column<List<string>>(type: "text[]", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkCrypterImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkCrypterImports_LinkCrypterRegistrations_LinkCrypterRegi~",
                        column: x => x.LinkCrypterRegistrationId,
                        principalTable: "LinkCrypterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_LinkCrypterImports_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "LinkCrypterImportContainers",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    LinkCrypterImportId = table.Column<int>(type: "integer", nullable: false),
                    ContainerId = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    ContainerUrl = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    ContainerName = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    ExternalReference = table.Column<string>(type: "text", nullable: false),
                    HasBackupLinks = table.Column<bool>(type: "boolean", nullable: false),
                    IsSelected = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    NotImportableReason = table.Column<int>(type: "integer", nullable: true),
                    ImportedReleaseId = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessages = table.Column<List<string>>(type: "text[]", nullable: false),
                    PreviewJson = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkCrypterImportContainers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkCrypterImportContainers_LinkCrypterImports_LinkCrypterI~",
                        column: x => x.LinkCrypterImportId,
                        principalTable: "LinkCrypterImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_LinkCrypterImportContainers_Releases_ImportedReleaseId",
                        column: x => x.ImportedReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "LinkCrypterImportHosterRegistrations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    LinkCrypterImportId = table.Column<int>(type: "integer", nullable: false),
                    HosterClassName = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    HosterRegistrationId = table.Column<int>(type: "integer", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkCrypterImportHosterRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkCrypterImportHosterRegistrations_HosterRegistrations_Ho~",
                        column: x => x.HosterRegistrationId,
                        principalTable: "HosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_LinkCrypterImportHosterRegistrations_LinkCrypterImports_Lin~",
                        column: x => x.LinkCrypterImportId,
                        principalTable: "LinkCrypterImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterImportContainers_ImportedReleaseId",
                table: "LinkCrypterImportContainers",
                column: "ImportedReleaseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterImportContainers_LinkCrypterImportId_Status",
                table: "LinkCrypterImportContainers",
                columns: new[] { "LinkCrypterImportId", "Status" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterImportHosterRegistrations_HosterRegistrationId",
                table: "LinkCrypterImportHosterRegistrations",
                column: "HosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterImportHosterRegistrations_LinkCrypterImportId_Ho~",
                table: "LinkCrypterImportHosterRegistrations",
                columns: new[] { "LinkCrypterImportId", "HosterClassName" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterImports_LinkCrypterRegistrationId",
                table: "LinkCrypterImports",
                column: "LinkCrypterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterImports_ReleaseTemplateId",
                table: "LinkCrypterImports",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterImports_State",
                table: "LinkCrypterImports",
                column: "State"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LinkCrypterImportContainers");

            migrationBuilder.DropTable(name: "LinkCrypterImportHosterRegistrations");

            migrationBuilder.DropTable(name: "LinkCrypterImports");
        }
    }
}
