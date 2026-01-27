using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class RemoveContainerName : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ContainerName",
            table: "UploadConfigLinkCrypters");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ContainerName",
            table: "UploadConfigLinkCrypters",
            type: "character varying(300)",
            maxLength: 300,
            nullable: false,
            defaultValue: "");
    }
}
