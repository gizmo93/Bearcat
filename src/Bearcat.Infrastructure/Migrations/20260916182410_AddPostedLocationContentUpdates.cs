using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostedLocationContentUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ContentUpdatedAt",
                table: "PostedLocations",
                type: "timestamp(4) without time zone",
                precision: 4,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "ForumPostTemplateId",
                table: "PostedLocations",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_PostedLocations_ForumPostTemplateId",
                table: "PostedLocations",
                column: "ForumPostTemplateId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_PostedLocations_ForumPostTemplates_ForumPostTemplateId",
                table: "PostedLocations",
                column: "ForumPostTemplateId",
                principalTable: "ForumPostTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PostedLocations_ForumPostTemplates_ForumPostTemplateId",
                table: "PostedLocations"
            );

            migrationBuilder.DropIndex(
                name: "IX_PostedLocations_ForumPostTemplateId",
                table: "PostedLocations"
            );

            migrationBuilder.DropColumn(name: "ContentUpdatedAt", table: "PostedLocations");

            migrationBuilder.DropColumn(name: "ForumPostTemplateId", table: "PostedLocations");
        }
    }
}
