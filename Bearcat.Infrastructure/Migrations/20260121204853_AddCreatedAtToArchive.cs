using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddCreatedAtToArchive : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "Archives",
            type: "timestamp(4) with time zone",
            precision: 4,
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "Archives");
    }
}
