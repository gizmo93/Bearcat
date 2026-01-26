using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "UploadConfigLinkCrypters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExternalReference",
                table: "LinkCrypterContainers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<List<string>>(
                name: "Errors",
                table: "LinkCrypterContainers",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "LinkCrypterContainers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "LinkCrypterContainers",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Password",
                table: "UploadConfigLinkCrypters");

            migrationBuilder.DropColumn(
                name: "Errors",
                table: "LinkCrypterContainers");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "LinkCrypterContainers");

            migrationBuilder.DropColumn(
                name: "State",
                table: "LinkCrypterContainers");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalReference",
                table: "LinkCrypterContainers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
