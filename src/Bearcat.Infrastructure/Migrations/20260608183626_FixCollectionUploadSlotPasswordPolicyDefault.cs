using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixCollectionUploadSlotPasswordPolicyDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "UploadConfigTemplates"
                SET "CollectionUploadSlotPasswordPolicy" = 1
                WHERE "CollectionUploadSlotPasswordPolicy" = 0;

                UPDATE "CollectionUploadSlots"
                SET "PasswordPolicy" = 1
                WHERE "PasswordPolicy" = 0;
                """
            );

            migrationBuilder.AlterColumn<int>(
                name: "CollectionUploadSlotPasswordPolicy",
                table: "UploadConfigTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CollectionUploadSlotPasswordPolicy",
                table: "UploadConfigTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1
            );
        }
    }
}
