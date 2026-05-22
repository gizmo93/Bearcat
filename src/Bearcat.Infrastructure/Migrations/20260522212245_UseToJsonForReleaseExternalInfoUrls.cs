using System.Collections.Generic;
using Bearcat.Domain.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UseToJsonForReleaseExternalInfoUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Urls",
                table: "ReleaseExternalInfos",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(List<ReleaseExternalInfoUrl>),
                oldType: "jsonb"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<List<ReleaseExternalInfoUrl>>(
                name: "Urls",
                table: "ReleaseExternalInfos",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true
            );
        }
    }
}
