using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundTaskInterval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "DefaultInterval",
                table: "BackgroundTaskStates",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0)
            );

            migrationBuilder.AddColumn<TimeSpan>(
                name: "IntervalOverride",
                table: "BackgroundTaskStates",
                type: "interval",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DefaultInterval", table: "BackgroundTaskStates");

            migrationBuilder.DropColumn(name: "IntervalOverride", table: "BackgroundTaskStates");
        }
    }
}
