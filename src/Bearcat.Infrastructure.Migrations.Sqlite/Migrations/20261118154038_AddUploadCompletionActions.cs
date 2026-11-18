using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddUploadCompletionActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UploadCompletionActionRegistrations",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ActionClassName = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: false
                    ),
                    SerializedConfig = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RunForInitialUpload = table.Column<bool>(type: "INTEGER", nullable: false),
                    RunForReupload = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasUnreadableSecrets = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", precision: 4, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadCompletionActionRegistrations", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "UploadCompletionActionExecutions",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReleaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadCompletionActionRegistrationId = table.Column<int>(
                        type: "INTEGER",
                        nullable: false
                    ),
                    HandledUploadedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: false
                    ),
                    ExecutedAt = table.Column<DateTime>(
                        type: "TEXT",
                        precision: 4,
                        nullable: false
                    ),
                    ResultText = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2000,
                        nullable: true
                    ),
                    ErrorMessage = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2000,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadCompletionActionExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadCompletionActionExecutions_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UploadCompletionActionExecutions_UploadCompletionActionRegistrations_UploadCompletionActionRegistrationId",
                        column: x => x.UploadCompletionActionRegistrationId,
                        principalTable: "UploadCompletionActionRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadCompletionActionExecutions_ReleaseId_UploadCompletionActionRegistrationId",
                table: "UploadCompletionActionExecutions",
                columns: new[] { "ReleaseId", "UploadCompletionActionRegistrationId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadCompletionActionExecutions_UploadCompletionActionRegistrationId",
                table: "UploadCompletionActionExecutions",
                column: "UploadCompletionActionRegistrationId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UploadCompletionActionExecutions");

            migrationBuilder.DropTable(name: "UploadCompletionActionRegistrations");
        }
    }
}
