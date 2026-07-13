using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHosterReuploadOverridesAndFullyOfflineSince : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FullyOfflineSince",
                table: "Uploads",
                type: "timestamp(4) without time zone",
                precision: 4,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "NumberOfHoursUntilReuploadOverride",
                table: "HosterRegistrations",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "ReuploadTriggerOverride",
                table: "HosterRegistrations",
                type: "integer",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FullyOfflineSince", table: "Uploads");

            migrationBuilder.DropColumn(
                name: "NumberOfHoursUntilReuploadOverride",
                table: "HosterRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "ReuploadTriggerOverride",
                table: "HosterRegistrations"
            );
        }
    }
}
