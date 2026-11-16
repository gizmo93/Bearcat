using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
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
                type: "timestamp(4) without time zone",
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
