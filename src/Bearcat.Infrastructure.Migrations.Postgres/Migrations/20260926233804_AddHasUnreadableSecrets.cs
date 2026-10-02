using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddHasUnreadableSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasUnreadableSecrets",
                table: "TelegramConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "HasUnreadableSecrets",
                table: "RemoteSourceRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "HasUnreadableSecrets",
                table: "NfoDatabaseRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "HasUnreadableSecrets",
                table: "MediaDatabaseRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "HasUnreadableSecrets",
                table: "LinkCrypterRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "HasUnreadableSecrets",
                table: "ImageHosterRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "HasUnreadableSecrets",
                table: "HosterRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "HasUnreadableSecrets",
                table: "DistributionSiteRegistrations",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasUnreadableSecrets",
                table: "TelegramConfigurations"
            );

            migrationBuilder.DropColumn(
                name: "HasUnreadableSecrets",
                table: "RemoteSourceRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "HasUnreadableSecrets",
                table: "NfoDatabaseRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "HasUnreadableSecrets",
                table: "MediaDatabaseRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "HasUnreadableSecrets",
                table: "LinkCrypterRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "HasUnreadableSecrets",
                table: "ImageHosterRegistrations"
            );

            migrationBuilder.DropColumn(name: "HasUnreadableSecrets", table: "HosterRegistrations");

            migrationBuilder.DropColumn(
                name: "HasUnreadableSecrets",
                table: "DistributionSiteRegistrations"
            );
        }
    }
}
