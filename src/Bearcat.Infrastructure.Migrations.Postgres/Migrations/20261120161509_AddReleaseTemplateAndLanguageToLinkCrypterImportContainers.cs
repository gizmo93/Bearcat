using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddReleaseTemplateAndLanguageToLinkCrypterImportContainers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeriesReleaseTemplateId",
                table: "LinkCrypterImports",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "PrimaryLanguageCode",
                table: "LinkCrypterImportContainers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "ReleaseTemplateId",
                table: "LinkCrypterImportContainers",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.Sql(
                """
                UPDATE "LinkCrypterImportContainers"
                SET "ReleaseTemplateId" = (
                        SELECT "ReleaseTemplateId" FROM "LinkCrypterImports"
                        WHERE "LinkCrypterImports"."Id" = "LinkCrypterImportContainers"."LinkCrypterImportId"
                    ),
                    "PrimaryLanguageCode" = (
                        SELECT "PrimaryLanguageCode" FROM "LinkCrypterImports"
                        WHERE "LinkCrypterImports"."Id" = "LinkCrypterImportContainers"."LinkCrypterImportId"
                    );
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterImports_SeriesReleaseTemplateId",
                table: "LinkCrypterImports",
                column: "SeriesReleaseTemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterImportContainers_ReleaseTemplateId",
                table: "LinkCrypterImportContainers",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LinkCrypterImportContainers_ReleaseTemplates_ReleaseTemplat~",
                table: "LinkCrypterImportContainers",
                column: "ReleaseTemplateId",
                principalTable: "ReleaseTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LinkCrypterImports_ReleaseTemplates_SeriesReleaseTemplateId",
                table: "LinkCrypterImports",
                column: "SeriesReleaseTemplateId",
                principalTable: "ReleaseTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LinkCrypterImportContainers_ReleaseTemplates_ReleaseTemplat~",
                table: "LinkCrypterImportContainers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LinkCrypterImports_ReleaseTemplates_SeriesReleaseTemplateId",
                table: "LinkCrypterImports"
            );

            migrationBuilder.DropIndex(
                name: "IX_LinkCrypterImports_SeriesReleaseTemplateId",
                table: "LinkCrypterImports"
            );

            migrationBuilder.DropIndex(
                name: "IX_LinkCrypterImportContainers_ReleaseTemplateId",
                table: "LinkCrypterImportContainers"
            );

            migrationBuilder.DropColumn(
                name: "SeriesReleaseTemplateId",
                table: "LinkCrypterImports"
            );

            migrationBuilder.DropColumn(
                name: "PrimaryLanguageCode",
                table: "LinkCrypterImportContainers"
            );

            migrationBuilder.DropColumn(
                name: "ReleaseTemplateId",
                table: "LinkCrypterImportContainers"
            );
        }
    }
}
