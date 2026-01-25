using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Core.Migrations;

/// <inheritdoc />
public partial class MakeFieldNonNullable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<List<string>>(
            name: "ErrorMessages",
            table: "UploadedFiles",
            type: "text[]",
            nullable: false,
            oldClrType: typeof(List<string>),
            oldType: "text[]",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<List<string>>(
            name: "ErrorMessages",
            table: "UploadedFiles",
            type: "text[]",
            nullable: true,
            oldClrType: typeof(List<string>),
            oldType: "text[]");
    }
}
