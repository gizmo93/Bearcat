using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "QualityGateEvaluatedAt",
                table: "Releases",
                type: "timestamp(4) without time zone",
                precision: 4,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "QualityGateState",
                table: "Releases",
                type: "integer",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<int>(
                name: "QualityProfileId",
                table: "ReleaseGroups",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "QualityProfiles",
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
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityProfiles", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "ReleaseQualityIssues",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseId = table.Column<int>(type: "integer", nullable: false),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseQualityIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseQualityIssues_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "QualityCheckRules",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    QualityProfileId = table.Column<int>(type: "integer", nullable: false),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    ParametersJson = table.Column<string>(type: "text", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityCheckRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualityCheckRules_QualityProfiles_QualityProfileId",
                        column: x => x.QualityProfileId,
                        principalTable: "QualityProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Releases_QualityGateState",
                table: "Releases",
                column: "QualityGateState"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseGroups_QualityProfileId",
                table: "ReleaseGroups",
                column: "QualityProfileId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_QualityCheckRules_QualityProfileId",
                table: "QualityCheckRules",
                column: "QualityProfileId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseQualityIssues_ReleaseId",
                table: "ReleaseQualityIssues",
                column: "ReleaseId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseGroups_QualityProfiles_QualityProfileId",
                table: "ReleaseGroups",
                column: "QualityProfileId",
                principalTable: "QualityProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseGroups_QualityProfiles_QualityProfileId",
                table: "ReleaseGroups"
            );

            migrationBuilder.DropTable(name: "QualityCheckRules");

            migrationBuilder.DropTable(name: "ReleaseQualityIssues");

            migrationBuilder.DropTable(name: "QualityProfiles");

            migrationBuilder.DropIndex(name: "IX_Releases_QualityGateState", table: "Releases");

            migrationBuilder.DropIndex(
                name: "IX_ReleaseGroups_QualityProfileId",
                table: "ReleaseGroups"
            );

            migrationBuilder.DropColumn(name: "QualityGateEvaluatedAt", table: "Releases");

            migrationBuilder.DropColumn(name: "QualityGateState", table: "Releases");

            migrationBuilder.DropColumn(name: "QualityProfileId", table: "ReleaseGroups");
        }
    }
}
