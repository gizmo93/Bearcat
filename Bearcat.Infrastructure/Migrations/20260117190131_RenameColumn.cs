using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class RenameColumn : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "HosterFullClassName",
            table: "HosterRegistrations",
            newName: "HosterClassName");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "HosterClassName",
            table: "HosterRegistrations",
            newName: "HosterFullClassName");
    }
}
