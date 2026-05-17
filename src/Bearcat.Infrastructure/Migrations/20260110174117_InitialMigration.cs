using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BearCat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "HosterRegistrations",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                Name = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                SerializedConfig = table.Column<string>(
                    type: "character varying(2000)",
                    maxLength: 2000,
                    nullable: false
                ),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                HosterFullClassName = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HosterRegistrations", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "Releases",
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
                ReleaseType = table.Column<int>(type: "integer", nullable: false),
                ReleaseFolderPath = table.Column<string>(
                    type: "character varying(1000)",
                    maxLength: 1000,
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Releases", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "ArchiveConfigs",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                ReleaseId = table.Column<int>(type: "integer", nullable: false),
                ArchiverFullClassName = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false
                ),
                ArchiveNamePrefix = table.Column<string>(
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
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArchiveConfigs", x => x.Id);
                table.ForeignKey(
                    name: "FK_ArchiveConfigs_Releases_ReleaseId",
                    column: x => x.ReleaseId,
                    principalTable: "Releases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "Archives",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                ArchiveConfigId = table.Column<int>(type: "integer", nullable: false),
                ArchiveFolderPath = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Archives", x => x.Id);
                table.ForeignKey(
                    name: "FK_Archives_ArchiveConfigs_ArchiveConfigId",
                    column: x => x.ArchiveConfigId,
                    principalTable: "ArchiveConfigs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "UploadConfigs",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                ReleaseId = table.Column<int>(type: "integer", nullable: false),
                HosterRegistrationId = table.Column<int>(type: "integer", nullable: false),
                ArchiveConfigId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UploadConfigs", x => x.Id);
                table.ForeignKey(
                    name: "FK_UploadConfigs_ArchiveConfigs_ArchiveConfigId",
                    column: x => x.ArchiveConfigId,
                    principalTable: "ArchiveConfigs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_UploadConfigs_HosterRegistrations_HosterRegistrationId",
                    column: x => x.HosterRegistrationId,
                    principalTable: "HosterRegistrations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_UploadConfigs_Releases_ReleaseId",
                    column: x => x.ReleaseId,
                    principalTable: "Releases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "ArchiveFiles",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                ArchiveId = table.Column<int>(type: "integer", nullable: false),
                FullFileName = table.Column<string>(
                    type: "character varying(1000)",
                    maxLength: 1000,
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArchiveFiles", x => x.Id);
                table.ForeignKey(
                    name: "FK_ArchiveFiles_Archives_ArchiveId",
                    column: x => x.ArchiveId,
                    principalTable: "Archives",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "Uploads",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                UploadConfigId = table.Column<int>(type: "integer", nullable: false),
                ArchiveId = table.Column<int>(type: "integer", nullable: true),
                CreatedAt = table.Column<DateTime>(
                    type: "timestamp(4) with time zone",
                    precision: 4,
                    nullable: false
                ),
                UploadedAt = table.Column<DateTime>(
                    type: "timestamp(4) with time zone",
                    precision: 4,
                    nullable: true
                ),
                UploadState = table.Column<int>(type: "integer", nullable: false),
                OnlineState = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Uploads", x => x.Id);
                table.ForeignKey(
                    name: "FK_Uploads_Archives_ArchiveId",
                    column: x => x.ArchiveId,
                    principalTable: "Archives",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull
                );
                table.ForeignKey(
                    name: "FK_Uploads_UploadConfigs_UploadConfigId",
                    column: x => x.UploadConfigId,
                    principalTable: "UploadConfigs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "UploadedFiles",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "integer", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                UploadId = table.Column<int>(type: "integer", nullable: false),
                ArchiveFileId = table.Column<int>(type: "integer", nullable: false),
                HosterFileLink = table.Column<string>(
                    type: "character varying(500)",
                    maxLength: 500,
                    nullable: false
                ),
                OnlineState = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(
                    type: "timestamp(4) with time zone",
                    precision: 4,
                    nullable: false
                ),
                CheckedAt = table.Column<DateTime>(
                    type: "timestamp(4) with time zone",
                    precision: 4,
                    nullable: true
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UploadedFiles", x => x.Id);
                table.ForeignKey(
                    name: "FK_UploadedFiles_ArchiveFiles_ArchiveFileId",
                    column: x => x.ArchiveFileId,
                    principalTable: "ArchiveFiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_UploadedFiles_Uploads_UploadId",
                    column: x => x.UploadId,
                    principalTable: "Uploads",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_ArchiveConfigs_ReleaseId",
            table: "ArchiveConfigs",
            column: "ReleaseId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_ArchiveFiles_ArchiveId",
            table: "ArchiveFiles",
            column: "ArchiveId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_Archives_ArchiveConfigId",
            table: "Archives",
            column: "ArchiveConfigId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_UploadConfigs_ArchiveConfigId",
            table: "UploadConfigs",
            column: "ArchiveConfigId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_UploadConfigs_HosterRegistrationId",
            table: "UploadConfigs",
            column: "HosterRegistrationId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_UploadConfigs_ReleaseId",
            table: "UploadConfigs",
            column: "ReleaseId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_UploadedFiles_ArchiveFileId",
            table: "UploadedFiles",
            column: "ArchiveFileId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_UploadedFiles_UploadId",
            table: "UploadedFiles",
            column: "UploadId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_Uploads_ArchiveId",
            table: "Uploads",
            column: "ArchiveId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_Uploads_UploadConfigId",
            table: "Uploads",
            column: "UploadConfigId"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "UploadedFiles");

        migrationBuilder.DropTable(name: "ArchiveFiles");

        migrationBuilder.DropTable(name: "Uploads");

        migrationBuilder.DropTable(name: "Archives");

        migrationBuilder.DropTable(name: "UploadConfigs");

        migrationBuilder.DropTable(name: "ArchiveConfigs");

        migrationBuilder.DropTable(name: "HosterRegistrations");

        migrationBuilder.DropTable(name: "Releases");
    }
}
