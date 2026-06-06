using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImageUploadConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImageUploadConfigs",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseId = table.Column<int>(type: "integer", nullable: false),
                    ImageHosterRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageUploadConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageUploadConfigs_ImageHosterRegistrations_ImageHosterRegi~",
                        column: x => x.ImageHosterRegistrationId,
                        principalTable: "ImageHosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ImageUploadConfigs_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ImageUploadConfigTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseTemplateId = table.Column<int>(type: "integer", nullable: false),
                    ImageHosterRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageUploadConfigTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageUploadConfigTemplates_ImageHosterRegistrations_ImageHo~",
                        column: x => x.ImageHosterRegistrationId,
                        principalTable: "ImageHosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ImageUploadConfigTemplates_ReleaseTemplates_ReleaseTemplate~",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ImageUploads",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ImageUploadConfigId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: false
                    ),
                    UploadedAt = table.Column<DateTime>(
                        type: "timestamp without time zone",
                        nullable: true
                    ),
                    UploadState = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessages = table.Column<string>(type: "jsonb", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageUploads_ImageUploadConfigs_ImageUploadConfigId",
                        column: x => x.ImageUploadConfigId,
                        principalTable: "ImageUploadConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ImageUploadUrls",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ImageUploadId = table.Column<int>(type: "integer", nullable: false),
                    ImageSize = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageUploadUrls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageUploadUrls_ImageUploads_ImageUploadId",
                        column: x => x.ImageUploadId,
                        principalTable: "ImageUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadConfigs_ImageHosterRegistrationId",
                table: "ImageUploadConfigs",
                column: "ImageHosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadConfigs_ReleaseId",
                table: "ImageUploadConfigs",
                column: "ReleaseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadConfigTemplates_ImageHosterRegistrationId",
                table: "ImageUploadConfigTemplates",
                column: "ImageHosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadConfigTemplates_ReleaseTemplateId",
                table: "ImageUploadConfigTemplates",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploads_ImageUploadConfigId",
                table: "ImageUploads",
                column: "ImageUploadConfigId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadUrls_ImageUploadId",
                table: "ImageUploadUrls",
                column: "ImageUploadId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ImageUploadConfigTemplates");

            migrationBuilder.DropTable(name: "ImageUploadUrls");

            migrationBuilder.DropTable(name: "ImageUploads");

            migrationBuilder.DropTable(name: "ImageUploadConfigs");
        }
    }
}
