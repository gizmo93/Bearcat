using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImageHosterRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImageHosterRegistrations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    Name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    ImageHosterClassName = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    SerializedConfig = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: false
                    ),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageHosterRegistrations", x => x.Id);
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ImageHosterRegistrations");
        }
    }
}
