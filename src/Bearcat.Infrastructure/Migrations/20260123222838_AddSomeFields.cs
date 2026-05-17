using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddSomeFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<List<string>>(
            name: "LinksDistributedTo",
            table: "UploadConfigs",
            type: "text[]",
            nullable: false
        );

        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "ArchiveConfigs",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: ""
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "LinksDistributedTo", table: "UploadConfigs");

        migrationBuilder.DropColumn(name: "Name", table: "ArchiveConfigs");
    }
}
