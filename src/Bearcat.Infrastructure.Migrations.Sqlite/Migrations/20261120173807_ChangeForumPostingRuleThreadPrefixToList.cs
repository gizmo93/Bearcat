using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class ChangeForumPostingRuleThreadPrefixToList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThreadPrefixIds",
                table: "ForumPostingRules",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]"
            );

            migrationBuilder.Sql(
                """
                UPDATE "ForumPostingRules"
                SET "ThreadPrefixIds" = json_array("ThreadPrefixId")
                WHERE "ThreadPrefixId" IS NOT NULL;
                """
            );

            migrationBuilder.DropColumn(name: "ThreadPrefixId", table: "ForumPostingRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThreadPrefixId",
                table: "ForumPostingRules",
                type: "TEXT",
                maxLength: 100,
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE "ForumPostingRules"
                SET "ThreadPrefixId" = json_extract("ThreadPrefixIds", '$[0]');
                """
            );

            migrationBuilder.DropColumn(name: "ThreadPrefixIds", table: "ForumPostingRules");
        }
    }
}
