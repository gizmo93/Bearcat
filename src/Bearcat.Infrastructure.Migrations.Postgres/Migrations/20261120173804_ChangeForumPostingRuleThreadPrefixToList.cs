using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ChangeForumPostingRuleThreadPrefixToList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "ThreadPrefixIds",
                table: "ForumPostingRules",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]"
            );

            migrationBuilder.Sql(
                """
                UPDATE "ForumPostingRules"
                SET "ThreadPrefixIds" = ARRAY["ThreadPrefixId"]::text[]
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
                type: "character varying(100)",
                maxLength: 100,
                nullable: true
            );

            migrationBuilder.Sql(
                """
                UPDATE "ForumPostingRules"
                SET "ThreadPrefixId" = "ThreadPrefixIds"[1]
                WHERE cardinality("ThreadPrefixIds") > 0;
                """
            );

            migrationBuilder.DropColumn(name: "ThreadPrefixIds", table: "ForumPostingRules");
        }
    }
}
