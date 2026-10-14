using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddHosterAccountPools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HosterAccountPools",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    HosterClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: false
                    ),
                    MinimumFreeSpaceBytes = table.Column<long>(
                        type: "INTEGER",
                        nullable: false,
                        defaultValue: 0L
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HosterAccountPools", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "HosterAccountPoolMembers",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HosterAccountPoolId = table.Column<int>(type: "INTEGER", nullable: false),
                    HosterRegistrationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HosterAccountPoolMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HosterAccountPoolMembers_HosterAccountPools_HosterAccountPoolId",
                        column: x => x.HosterAccountPoolId,
                        principalTable: "HosterAccountPools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_HosterAccountPoolMembers_HosterRegistrations_HosterRegistrationId",
                        column: x => x.HosterRegistrationId,
                        principalTable: "HosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_HosterAccountPoolMembers_HosterAccountPoolId_HosterRegistrationId",
                table: "HosterAccountPoolMembers",
                columns: new[] { "HosterAccountPoolId", "HosterRegistrationId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_HosterAccountPoolMembers_HosterRegistrationId",
                table: "HosterAccountPoolMembers",
                column: "HosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_HosterAccountPools_Name",
                table: "HosterAccountPools",
                column: "Name",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HosterAccountPoolMembers");

            migrationBuilder.DropTable(name: "HosterAccountPools");
        }
    }
}
