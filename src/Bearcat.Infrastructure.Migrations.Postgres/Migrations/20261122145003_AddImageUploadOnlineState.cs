using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddImageUploadOnlineState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OfflineSince",
                table: "ImageUploads",
                type: "timestamp without time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "OnlineCheckedAt",
                table: "ImageUploads",
                type: "timestamp without time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "OnlineState",
                table: "ImageUploads",
                type: "integer",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploads_OnlineCheckedAt",
                table: "ImageUploads",
                column: "OnlineCheckedAt"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImageUploads_OnlineCheckedAt",
                table: "ImageUploads"
            );

            migrationBuilder.DropColumn(name: "OfflineSince", table: "ImageUploads");

            migrationBuilder.DropColumn(name: "OnlineCheckedAt", table: "ImageUploads");

            migrationBuilder.DropColumn(name: "OnlineState", table: "ImageUploads");
        }
    }
}
