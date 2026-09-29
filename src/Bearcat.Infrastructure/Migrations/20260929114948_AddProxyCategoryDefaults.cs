using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProxyCategoryDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProxyCategoryDefaults",
                columns: table => new
                {
                    ProxyCategory = table.Column<int>(type: "integer", nullable: false),
                    ProxyServerId = table.Column<int>(type: "integer", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProxyCategoryDefaults", x => x.ProxyCategory);
                    table.ForeignKey(
                        name: "FK_ProxyCategoryDefaults_ProxyServers_ProxyServerId",
                        column: x => x.ProxyServerId,
                        principalTable: "ProxyServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ProxyServers_Host_Port",
                table: "ProxyServers",
                columns: new[] { "Host", "Port" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ProxyCategoryDefaults_ProxyServerId",
                table: "ProxyCategoryDefaults",
                column: "ProxyServerId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProxyCategoryDefaults");

            migrationBuilder.DropIndex(name: "IX_ProxyServers_Host_Port", table: "ProxyServers");
        }
    }
}
