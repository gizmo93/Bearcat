using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteSourceAutomations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemoteSourceAutomations",
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
                    RemoteSourceRegistrationId = table.Column<int>(
                        type: "integer",
                        nullable: false
                    ),
                    RemotePath = table.Column<string>(type: "text", nullable: false),
                    TargetPath = table.Column<string>(type: "text", nullable: false),
                    FolderNamePattern = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: true
                    ),
                    ReleaseTemplateId = table.Column<int>(type: "integer", nullable: false),
                    PrimaryLanguageCode = table.Column<string>(
                        type: "character varying(2)",
                        maxLength: 2,
                        nullable: true
                    ),
                    KeepRawFiles = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IgnoreExistingOnFirstScan = table.Column<bool>(
                        type: "boolean",
                        nullable: false
                    ),
                    HasCompletedInitialScan = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteSourceAutomations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteSourceAutomations_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_RemoteSourceAutomations_RemoteSourceRegistrations_RemoteSou~",
                        column: x => x.RemoteSourceRegistrationId,
                        principalTable: "RemoteSourceRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "RemoteSourceDownloads",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    RemoteSourceAutomationId = table.Column<int>(type: "integer", nullable: true),
                    RemoteSourceRegistrationId = table.Column<int>(type: "integer", nullable: true),
                    SourceName = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    RemoteFolderPath = table.Column<string>(type: "text", nullable: false),
                    FolderName = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    LocalFolderPath = table.Column<string>(type: "text", nullable: false),
                    ReleaseTemplateId = table.Column<int>(type: "integer", nullable: true),
                    PrimaryLanguageCode = table.Column<string>(
                        type: "character varying(2)",
                        maxLength: 2,
                        nullable: true
                    ),
                    KeepRawFiles = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    FileCount = table.Column<int>(type: "integer", nullable: false),
                    TotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    LastChangedAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: false
                    ),
                    DiscoveredAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: false
                    ),
                    StartedAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: true
                    ),
                    CompletedAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: true
                    ),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ReleaseId = table.Column<int>(type: "integer", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteSourceDownloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteSourceDownloads_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_RemoteSourceDownloads_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_RemoteSourceDownloads_RemoteSourceAutomations_RemoteSourceA~",
                        column: x => x.RemoteSourceAutomationId,
                        principalTable: "RemoteSourceAutomations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_RemoteSourceDownloads_RemoteSourceRegistrations_RemoteSourc~",
                        column: x => x.RemoteSourceRegistrationId,
                        principalTable: "RemoteSourceRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceAutomations_ReleaseTemplateId",
                table: "RemoteSourceAutomations",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceAutomations_RemoteSourceRegistrationId_Priority",
                table: "RemoteSourceAutomations",
                columns: new[] { "RemoteSourceRegistrationId", "Priority" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceDownloads_ReleaseId",
                table: "RemoteSourceDownloads",
                column: "ReleaseId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceDownloads_ReleaseTemplateId",
                table: "RemoteSourceDownloads",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceDownloads_RemoteSourceAutomationId_State",
                table: "RemoteSourceDownloads",
                columns: new[] { "RemoteSourceAutomationId", "State" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSourceDownloads_RemoteSourceRegistrationId_RemoteFold~",
                table: "RemoteSourceDownloads",
                columns: new[] { "RemoteSourceRegistrationId", "RemoteFolderPath" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "RemoteSourceDownloads");

            migrationBuilder.DropTable(name: "RemoteSourceAutomations");
        }
    }
}
