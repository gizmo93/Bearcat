using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveNfoToReleaseAndAddExternalIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseNfos_ReleaseInfos_ReleaseInfoId",
                table: "ReleaseNfos"
            );

            migrationBuilder.DropIndex(name: "IX_ReleaseNfos_ReleaseInfoId", table: "ReleaseNfos");

            migrationBuilder.AlterColumn<int>(
                name: "ReleaseInfoId",
                table: "ReleaseNfos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer"
            );

            migrationBuilder.AddColumn<int>(
                name: "ReleaseId",
                table: "ReleaseNfos",
                type: "integer",
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE "ReleaseNfos" AS nfo
                SET "ReleaseId" = info."ReleaseId"
                FROM "ReleaseInfos" AS info
                WHERE info."Id" = nfo."ReleaseInfoId";
                """
            );

            migrationBuilder.AlterColumn<int>(
                name: "ReleaseId",
                table: "ReleaseNfos",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );

            migrationBuilder.CreateTable(
                name: "ReleaseExternalIdentifiers",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    Source = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseExternalIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseExternalIdentifiers_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseNfos_ReleaseId",
                table: "ReleaseNfos",
                column: "ReleaseId",
                unique: true
            );

            migrationBuilder.Sql(
                """
                INSERT INTO "ReleaseExternalIdentifiers" ("ReleaseId", "Type", "Value", "Source")
                SELECT DISTINCT nfo."ReleaseId", 1, lower((matches.id)[1]), 1
                FROM "ReleaseNfos" AS nfo
                CROSS JOIN LATERAL regexp_matches(nfo."Content", 'tt[0-9]{7,8}', 'gi') AS matches(id)
                ON CONFLICT DO NOTHING;

                INSERT INTO "ReleaseExternalIdentifiers" ("ReleaseId", "Type", "Value", "Source")
                SELECT DISTINCT
                    info."ReleaseId",
                    1,
                    lower((regexp_match(url.value->>'url', 'tt[0-9]{7,8}', 'i'))[1]),
                    CASE WHEN info."NfoDatabaseClassName" ILIKE '%srrdb%' THEN 3 ELSE 2 END
                FROM "ReleaseExternalInfos" AS external
                JOIN "ReleaseInfos" AS info ON info."Id" = external."ReleaseInfoId"
                CROSS JOIN LATERAL jsonb_array_elements(external."Urls") AS url(value)
                WHERE url.value->>'url' ~* 'tt[0-9]{7,8}'
                ON CONFLICT DO NOTHING;
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseExternalIdentifiers_ReleaseId_Type_Value_Source",
                table: "ReleaseExternalIdentifiers",
                columns: new[] { "ReleaseId", "Type", "Value", "Source" },
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseNfos_Releases_ReleaseId",
                table: "ReleaseNfos",
                column: "ReleaseId",
                principalTable: "Releases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReleaseNfos_Releases_ReleaseId",
                table: "ReleaseNfos"
            );

            migrationBuilder.DropTable(name: "ReleaseExternalIdentifiers");

            migrationBuilder.DropIndex(name: "IX_ReleaseNfos_ReleaseId", table: "ReleaseNfos");

            migrationBuilder.Sql(
                """
                INSERT INTO "ReleaseInfos" (
                    "ReleaseId",
                    "NfoDatabaseClassName",
                    "ReleaseName"
                )
                SELECT nfo."ReleaseId", 'Local', release."Name"
                FROM "ReleaseNfos" AS nfo
                JOIN "Releases" AS release ON release."Id" = nfo."ReleaseId"
                LEFT JOIN "ReleaseInfos" AS info ON info."ReleaseId" = nfo."ReleaseId"
                WHERE info."Id" IS NULL;

                UPDATE "ReleaseNfos" AS nfo
                SET "ReleaseInfoId" = info."Id"
                FROM "ReleaseInfos" AS info
                WHERE info."ReleaseId" = nfo."ReleaseId";
                """
            );

            migrationBuilder.DropColumn(name: "ReleaseId", table: "ReleaseNfos");

            migrationBuilder.AlterColumn<int>(
                name: "ReleaseInfoId",
                table: "ReleaseNfos",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseNfos_ReleaseInfoId",
                table: "ReleaseNfos",
                column: "ReleaseInfoId",
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ReleaseNfos_ReleaseInfos_ReleaseInfoId",
                table: "ReleaseNfos",
                column: "ReleaseInfoId",
                principalTable: "ReleaseInfos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
