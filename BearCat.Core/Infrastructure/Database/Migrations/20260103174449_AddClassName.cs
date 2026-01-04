using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Core.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddClassName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HosterFullClassName",
                table: "HosterRegistrations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HosterFullClassName",
                table: "HosterRegistrations");
        }
    }
}
