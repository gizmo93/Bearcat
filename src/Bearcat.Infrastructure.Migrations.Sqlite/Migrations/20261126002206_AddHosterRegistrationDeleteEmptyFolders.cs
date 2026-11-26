using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddHosterRegistrationDeleteEmptyFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeleteEmptyFolders",
                table: "HosterRegistrations",
                type: "INTEGER",
                nullable: false,
                defaultValue: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DeleteEmptyFolders", table: "HosterRegistrations");
        }
    }
}
