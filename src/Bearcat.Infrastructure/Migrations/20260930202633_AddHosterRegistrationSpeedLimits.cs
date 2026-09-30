using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHosterRegistrationSpeedLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MirrorDownloadSpeedLimitMegabytesPerSecond",
                table: "HosterRegistrations",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true
            );

            migrationBuilder.AddColumn<decimal>(
                name: "UploadSpeedLimitMegabytesPerSecond",
                table: "HosterRegistrations",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MirrorDownloadSpeedLimitMegabytesPerSecond",
                table: "HosterRegistrations"
            );

            migrationBuilder.DropColumn(
                name: "UploadSpeedLimitMegabytesPerSecond",
                table: "HosterRegistrations"
            );
        }
    }
}
