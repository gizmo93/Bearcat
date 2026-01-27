using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BearCat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LinkCrypterContainers_UploadConfigLinkCrypters_Id",
                table: "LinkCrypterContainers");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "LinkCrypterContainers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterContainers_UploadConfigLinkCrypterId",
                table: "LinkCrypterContainers",
                column: "UploadConfigLinkCrypterId");

            migrationBuilder.AddForeignKey(
                name: "FK_LinkCrypterContainers_UploadConfigLinkCrypters_UploadConfig~",
                table: "LinkCrypterContainers",
                column: "UploadConfigLinkCrypterId",
                principalTable: "UploadConfigLinkCrypters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LinkCrypterContainers_UploadConfigLinkCrypters_UploadConfig~",
                table: "LinkCrypterContainers");

            migrationBuilder.DropIndex(
                name: "IX_LinkCrypterContainers_UploadConfigLinkCrypterId",
                table: "LinkCrypterContainers");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "LinkCrypterContainers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddForeignKey(
                name: "FK_LinkCrypterContainers_UploadConfigLinkCrypters_Id",
                table: "LinkCrypterContainers",
                column: "Id",
                principalTable: "UploadConfigLinkCrypters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
