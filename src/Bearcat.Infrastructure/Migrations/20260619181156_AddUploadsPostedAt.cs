using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadsPostedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UploadsPostedAt",
                table: "Releases",
                type: "timestamp(4) without time zone",
                precision: 4,
                nullable: true
            );

            migrationBuilder.AlterColumn<DateTime>(
                name: "MetadataCheckedAt",
                table: "ReleaseCollections",
                type: "timestamp(4) without time zone",
                precision: 4,
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadsPostedAt",
                table: "ReleaseCollections",
                type: "timestamp(4) without time zone",
                precision: 4,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "UploadsPostedAt", table: "Releases");

            migrationBuilder.DropColumn(name: "UploadsPostedAt", table: "ReleaseCollections");

            migrationBuilder.AlterColumn<DateTime>(
                name: "MetadataCheckedAt",
                table: "ReleaseCollections",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp(4) without time zone",
                oldPrecision: 4,
                oldNullable: true
            );
        }
    }
}
