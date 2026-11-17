using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddForumServiceRestrictionsAndPostingFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "ForbiddenHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "ForbiddenImageHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "ForbiddenLinkCrypterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.AddColumn<int>(
                name: "MinimumSecondsBetweenPosts",
                table: "DistributionSiteRegistrations",
                type: "integer",
                nullable: false,
                defaultValue: 30
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "RequiredHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "RequiredImageHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.AddColumn<List<string>>(
                name: "RequiredLinkCrypterClassNames",
                table: "DistributionSiteRegistrations",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"
            );

            migrationBuilder.CreateTable(
                name: "ForumPostingFailures",
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
                    DistributionSiteRegistrationId = table.Column<int>(
                        type: "integer",
                        nullable: false
                    ),
                    FailedAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: true
                    ),
                    LastErrorMessage = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: false
                    ),
                    State = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumPostingFailures", x => x.Id);
                    table.CheckConstraint(
                        "CK_ForumPostingFailure_Owner",
                        "(\"ReleaseId\" IS NOT NULL) <> (\"ReleaseCollectionId\" IS NOT NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_ForumPostingFailures_DistributionSiteRegistrations_Distribu~",
                        column: x => x.DistributionSiteRegistrationId,
                        principalTable: "DistributionSiteRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ForumPostingFailures_ReleaseCollections_ReleaseCollectionId",
                        column: x => x.ReleaseCollectionId,
                        principalTable: "ReleaseCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ForumPostingFailures_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostingFailures_DistributionSiteRegistrationId",
                table: "ForumPostingFailures",
                column: "DistributionSiteRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostingFailures_ReleaseCollectionId_DistributionSiteRe~",
                table: "ForumPostingFailures",
                columns: new[] { "ReleaseCollectionId", "DistributionSiteRegistrationId" },
                unique: true,
                filter: "\"ReleaseCollectionId\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostingFailures_ReleaseId_DistributionSiteRegistration~",
                table: "ForumPostingFailures",
                columns: new[] { "ReleaseId", "DistributionSiteRegistrationId" },
                unique: true,
                filter: "\"ReleaseId\" IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ForumPostingFailures");

            migrationBuilder.DropColumn(
                name: "ForbiddenHosterClassNames",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "ForbiddenImageHosterClassNames",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "ForbiddenLinkCrypterClassNames",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "MinimumSecondsBetweenPosts",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "RequiredHosterClassNames",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "RequiredImageHosterClassNames",
                table: "DistributionSiteRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "RequiredLinkCrypterClassNames",
                table: "DistributionSiteRegistrations"
            );
        }
    }
}
