using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BearCat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReleaseTemplates",
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
                    ReleaseType = table.Column<int>(type: "integer", nullable: false),
                    ReleaseGroupId = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseTemplates_ReleaseGroups_ReleaseGroupId",
                        column: x => x.ReleaseGroupId,
                        principalTable: "ReleaseGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ArchiveConfigTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseTemplateId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    ArchiveFilesBasePath = table.Column<string>(
                        type: "character varying(300)",
                        maxLength: 300,
                        nullable: false
                    ),
                    ArchiverName = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    ArchivePassword = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    ArchiveFileSizeMb = table.Column<int>(type: "integer", nullable: false),
                    UseReleaseNameAsArchiveName = table.Column<bool>(
                        type: "boolean",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveConfigTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveConfigTemplates_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "UploadConfigTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseTemplateId = table.Column<int>(type: "integer", nullable: false),
                    ArchiveConfigTemplateId = table.Column<int>(type: "integer", nullable: false),
                    HosterRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: true
                    ),
                    LinksDistributedTo = table.Column<List<string>>(
                        type: "text[]",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadConfigTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadConfigTemplates_ArchiveConfigTemplates_ArchiveConfigT~",
                        column: x => x.ArchiveConfigTemplateId,
                        principalTable: "ArchiveConfigTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UploadConfigTemplates_HosterRegistrations_HosterRegistratio~",
                        column: x => x.HosterRegistrationId,
                        principalTable: "HosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UploadConfigTemplates_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "UploadConfigLinkCrypterTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    UploadConfigTemplateId = table.Column<int>(type: "integer", nullable: false),
                    LinkCrypterRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    Password = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadConfigLinkCrypterTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadConfigLinkCrypterTemplates_LinkCrypterRegistrations_L~",
                        column: x => x.LinkCrypterRegistrationId,
                        principalTable: "LinkCrypterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UploadConfigLinkCrypterTemplates_UploadConfigTemplates_Uplo~",
                        column: x => x.UploadConfigTemplateId,
                        principalTable: "UploadConfigTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveConfigTemplates_ReleaseTemplateId",
                table: "ArchiveConfigTemplates",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseTemplates_ReleaseGroupId",
                table: "ReleaseTemplates",
                column: "ReleaseGroupId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigLinkCrypterTemplates_LinkCrypterRegistrationId",
                table: "UploadConfigLinkCrypterTemplates",
                column: "LinkCrypterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigLinkCrypterTemplates_UploadConfigTemplateId",
                table: "UploadConfigLinkCrypterTemplates",
                column: "UploadConfigTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigTemplates_ArchiveConfigTemplateId",
                table: "UploadConfigTemplates",
                column: "ArchiveConfigTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigTemplates_HosterRegistrationId",
                table: "UploadConfigTemplates",
                column: "HosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigTemplates_ReleaseTemplateId",
                table: "UploadConfigTemplates",
                column: "ReleaseTemplateId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UploadConfigLinkCrypterTemplates");

            migrationBuilder.DropTable(name: "UploadConfigTemplates");

            migrationBuilder.DropTable(name: "ArchiveConfigTemplates");

            migrationBuilder.DropTable(name: "ReleaseTemplates");
        }
    }
}
