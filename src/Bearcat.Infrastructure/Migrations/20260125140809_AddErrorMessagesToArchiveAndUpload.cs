using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddErrorMessagesToArchiveAndUpload : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<List<string>>(
            name: "ErrorMessages",
            table: "Uploads",
            type: "text[]",
            nullable: false
        );

        migrationBuilder.AddColumn<int>(
            name: "ArchiveState",
            table: "Archives",
            type: "integer",
            nullable: false,
            defaultValue: 0
        );

        migrationBuilder.AddColumn<List<string>>(
            name: "ErrorMessages",
            table: "Archives",
            type: "text[]",
            nullable: false
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ErrorMessages", table: "Uploads");

        migrationBuilder.DropColumn(name: "ArchiveState", table: "Archives");

        migrationBuilder.DropColumn(name: "ErrorMessages", table: "Archives");
    }
}
