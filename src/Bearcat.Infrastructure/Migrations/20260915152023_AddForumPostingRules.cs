using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForumPostingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ForumPostingRules",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    DistributionSiteRegistrationId = table.Column<int>(
                        type: "integer",
                        nullable: false
                    ),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    ConditionJson = table.Column<string>(
                        type: "character varying(8000)",
                        maxLength: 8000,
                        nullable: false
                    ),
                    TargetNodeId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    TargetPathSnapshot = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    ThreadPrefixId = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    ForumPostTemplateId = table.Column<int>(type: "integer", nullable: false),
                    PostMode = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
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
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForumPostingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForumPostingRules_DistributionSiteRegistrations_Distributio~",
                        column: x => x.DistributionSiteRegistrationId,
                        principalTable: "DistributionSiteRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ForumPostingRules_ForumPostTemplates_ForumPostTemplateId",
                        column: x => x.ForumPostTemplateId,
                        principalTable: "ForumPostTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostingRules_DistributionSiteRegistrationId_SortOrder",
                table: "ForumPostingRules",
                columns: new[] { "DistributionSiteRegistrationId", "SortOrder" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ForumPostingRules_ForumPostTemplateId",
                table: "ForumPostingRules",
                column: "ForumPostTemplateId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ForumPostingRules");
        }
    }
}
