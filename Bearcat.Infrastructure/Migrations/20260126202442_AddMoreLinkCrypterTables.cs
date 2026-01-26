using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BearCat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreLinkCrypterTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UploadConfigLinkCrypters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UploadConfigId = table.Column<int>(type: "integer", nullable: false),
                    LinkCrypterRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    ContainerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadConfigLinkCrypters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadConfigLinkCrypters_LinkCrypterRegistrations_LinkCrypt~",
                        column: x => x.LinkCrypterRegistrationId,
                        principalTable: "LinkCrypterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UploadConfigLinkCrypters_UploadConfigs_UploadConfigId",
                        column: x => x.UploadConfigId,
                        principalTable: "UploadConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinkCrypterContainers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    UploadConfigLinkCrypterId = table.Column<int>(type: "integer", nullable: false),
                    UploadId = table.Column<int>(type: "integer", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContainerUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkCrypterContainers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainers_UploadConfigLinkCrypters_Id",
                        column: x => x.Id,
                        principalTable: "UploadConfigLinkCrypters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LinkCrypterContainers_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainers_UploadId",
                table: "LinkCrypterContainers",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigLinkCrypters_LinkCrypterRegistrationId",
                table: "UploadConfigLinkCrypters",
                column: "LinkCrypterRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigLinkCrypters_UploadConfigId",
                table: "UploadConfigLinkCrypters",
                column: "UploadConfigId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinkCrypterContainers");

            migrationBuilder.DropTable(
                name: "UploadConfigLinkCrypters");
        }
    }
}
