using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Core.Migrations;

/// <inheritdoc />
public partial class AddErrorMessagesToUploadedFile : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<List<string>>(
            name: "ErrorMessages",
            table: "UploadedFiles",
            type: "text[]",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ErrorMessages",
            table: "UploadedFiles");
    }
}
