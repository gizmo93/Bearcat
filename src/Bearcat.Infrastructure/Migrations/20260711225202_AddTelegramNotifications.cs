using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelegramConfigurations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    EncryptedBotToken = table.Column<string>(type: "text", nullable: false),
                    BotUsername = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: false
                    ),
                    NotificationBaseUrl = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: false
                    ),
                    ChatId = table.Column<long>(type: "bigint", nullable: true),
                    ChatName = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: true
                    ),
                    ForwardInfo = table.Column<bool>(type: "boolean", nullable: false),
                    ForwardWarning = table.Column<bool>(type: "boolean", nullable: false),
                    ForwardError = table.Column<bool>(type: "boolean", nullable: false),
                    PairingTokenHash = table.Column<string>(
                        type: "character varying(64)",
                        maxLength: 64,
                        nullable: true
                    ),
                    PairingExpiresAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: true
                    ),
                    UpdateOffset = table.Column<long>(type: "bigint", nullable: false),
                    ForwardNotificationsAfterId = table.Column<int>(
                        type: "integer",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramConfigurations", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "TelegramDeliveries",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    NotificationId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: false
                    ),
                    DeliveredAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: true
                    ),
                    NextAttemptAt = table.Column<DateTime>(
                        type: "timestamp(4) without time zone",
                        precision: 4,
                        nullable: true
                    ),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelegramDeliveries_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TelegramDeliveries_DeliveredAt_NextAttemptAt",
                table: "TelegramDeliveries",
                columns: new[] { "DeliveredAt", "NextAttemptAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TelegramDeliveries_NotificationId",
                table: "TelegramDeliveries",
                column: "NotificationId",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TelegramConfigurations");

            migrationBuilder.DropTable(name: "TelegramDeliveries");
        }
    }
}
