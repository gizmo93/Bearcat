using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BearCat.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CreatedAt = table.Column<DateTime>(type: "timestamp(4) with time zone", precision: 4, nullable: false),
                ResolvedAt = table.Column<DateTime>(type: "timestamp(4) with time zone", precision: 4, nullable: true),
                NotificationType = table.Column<int>(type: "integer", nullable: false),
                Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Notifications");
    }
}
