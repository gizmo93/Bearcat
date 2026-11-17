using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddForumServiceRestrictionsAndPostingFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ForbiddenHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<string>(
                name: "ForbiddenImageHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<string>(
                name: "ForbiddenLinkCrypterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<int>(
                name: "MinimumSecondsBetweenPosts",
                table: "DistributionSiteRegistrations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30
            );

            migrationBuilder.AddColumn<string>(
                name: "RequiredHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<string>(
                name: "RequiredImageHosterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.AddColumn<string>(
                name: "RequiredLinkCrypterClassNames",
                table: "DistributionSiteRegistrations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.CreateTable(
                name: "ForumPostingFailures",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReleaseCollectionId = table.Column<int>(type: "INTEGER", nullable: true),
                    DistributionSiteRegistrationId = table.Column<int>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    FailedAttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: true
                    ),
                    LastErrorMessage = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2000,
                        nullable: false
                    ),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumPostingFailures", x => x.Id);
                    table.CheckConstraint(
                        "CK_ForumPostingFailure_Owner",
                        "(\"ReleaseId\" IS NOT NULL) <> (\"ReleaseCollectionId\" IS NOT NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_ForumPostingFailures_DistributionSiteRegistrations_DistributionSiteRegistrationId",
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
                name: "IX_ForumPostingFailures_ReleaseCollectionId_DistributionSiteRegistrationId",
                table: "ForumPostingFailures",
                columns: new[] { "ReleaseCollectionId", "DistributionSiteRegistrationId" },
                unique: true,
                filter: "\"ReleaseCollectionId\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostingFailures_ReleaseId_DistributionSiteRegistrationId",
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
