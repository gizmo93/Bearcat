using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Core.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class LimitLengthOfArchiveNamePrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ArchiveNamePrefix",
                table: "Distributions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ArchiveNamePrefix",
                table: "Distributions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);
        }
    }
}
