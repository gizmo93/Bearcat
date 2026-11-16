using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddUploadPrioritizedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PrioritizedAt",
                table: "Uploads",
                type: "TEXT",
                precision: 4,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PrioritizedAt", table: "Uploads");
        }
    }
}
