using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BearCat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReleaseFolderAutomationEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReleaseFolderAutomations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BasePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FolderNamePattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReleaseTemplateId = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseFolderAutomations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseFolderAutomations_ReleaseTemplates_ReleaseTemplateId",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseFolderAutomations_ReleaseTemplateId",
                table: "ReleaseFolderAutomations",
                column: "ReleaseTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReleaseFolderAutomations");
        }
    }
}
