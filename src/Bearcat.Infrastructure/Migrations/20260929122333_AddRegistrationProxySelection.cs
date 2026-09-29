using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationProxySelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProxySelection",
                table: "LinkCrypterRegistrations",
                type: "integer",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<int>(
                name: "ProxyServerId",
                table: "LinkCrypterRegistrations",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "ProxySelection",
                table: "ImageHosterRegistrations",
                type: "integer",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<int>(
                name: "ProxyServerId",
                table: "ImageHosterRegistrations",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "MirrorDownloadProxySelection",
                table: "HosterRegistrations",
                type: "integer",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<int>(
                name: "MirrorDownloadProxyServerId",
                table: "HosterRegistrations",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "UploadProxySelection",
                table: "HosterRegistrations",
                type: "integer",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<int>(
                name: "UploadProxyServerId",
                table: "HosterRegistrations",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_LinkCrypterRegistrations_ProxyServerId",
                table: "LinkCrypterRegistrations",
                column: "ProxyServerId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageHosterRegistrations_ProxyServerId",
                table: "ImageHosterRegistrations",
                column: "ProxyServerId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_HosterRegistrations_MirrorDownloadProxyServerId",
                table: "HosterRegistrations",
                column: "MirrorDownloadProxyServerId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_HosterRegistrations_UploadProxyServerId",
                table: "HosterRegistrations",
                column: "UploadProxyServerId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_HosterRegistrations_ProxyServers_MirrorDownloadProxyServerId",
                table: "HosterRegistrations",
                column: "MirrorDownloadProxyServerId",
                principalTable: "ProxyServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_HosterRegistrations_ProxyServers_UploadProxyServerId",
                table: "HosterRegistrations",
                column: "UploadProxyServerId",
                principalTable: "ProxyServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ImageHosterRegistrations_ProxyServers_ProxyServerId",
                table: "ImageHosterRegistrations",
                column: "ProxyServerId",
                principalTable: "ProxyServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_LinkCrypterRegistrations_ProxyServers_ProxyServerId",
                table: "LinkCrypterRegistrations",
                column: "ProxyServerId",
                principalTable: "ProxyServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HosterRegistrations_ProxyServers_MirrorDownloadProxyServerId",
                table: "HosterRegistrations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_HosterRegistrations_ProxyServers_UploadProxyServerId",
                table: "HosterRegistrations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_ImageHosterRegistrations_ProxyServers_ProxyServerId",
                table: "ImageHosterRegistrations"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_LinkCrypterRegistrations_ProxyServers_ProxyServerId",
                table: "LinkCrypterRegistrations"
            );

            migrationBuilder.DropIndex(
                name: "IX_LinkCrypterRegistrations_ProxyServerId",
                table: "LinkCrypterRegistrations"
            );

            migrationBuilder.DropIndex(
                name: "IX_ImageHosterRegistrations_ProxyServerId",
                table: "ImageHosterRegistrations"
            );

            migrationBuilder.DropIndex(
                name: "IX_HosterRegistrations_MirrorDownloadProxyServerId",
                table: "HosterRegistrations"
            );

            migrationBuilder.DropIndex(
                name: "IX_HosterRegistrations_UploadProxyServerId",
                table: "HosterRegistrations"
            );

            migrationBuilder.DropColumn(name: "ProxySelection", table: "LinkCrypterRegistrations");

            migrationBuilder.DropColumn(name: "ProxyServerId", table: "LinkCrypterRegistrations");

            migrationBuilder.DropColumn(name: "ProxySelection", table: "ImageHosterRegistrations");

            migrationBuilder.DropColumn(name: "ProxyServerId", table: "ImageHosterRegistrations");

            migrationBuilder.DropColumn(
                name: "MirrorDownloadProxySelection",
                table: "HosterRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "MirrorDownloadProxyServerId",
                table: "HosterRegistrations"
            );

            migrationBuilder.DropColumn(name: "UploadProxySelection", table: "HosterRegistrations");

            migrationBuilder.DropColumn(name: "UploadProxyServerId", table: "HosterRegistrations");
        }
    }
}
