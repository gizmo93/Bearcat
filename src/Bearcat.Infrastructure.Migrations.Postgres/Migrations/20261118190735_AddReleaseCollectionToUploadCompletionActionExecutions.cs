using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddReleaseCollectionToUploadCompletionActionExecutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ReleaseId",
                table: "UploadCompletionActionExecutions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer"
            );

            migrationBuilder.AddColumn<int>(
                name: "ReleaseCollectionId",
                table: "UploadCompletionActionExecutions",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadCompletionActionExecutions_ReleaseCollectionId_Upload~",
                table: "UploadCompletionActionExecutions",
                columns: new[] { "ReleaseCollectionId", "UploadCompletionActionRegistrationId" },
                unique: true
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_UploadCompletionActionExecution_Owner",
                table: "UploadCompletionActionExecutions",
                sql: "(\"ReleaseId\" IS NOT NULL) <> (\"ReleaseCollectionId\" IS NOT NULL)"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_UploadCompletionActionExecutions_ReleaseCollections_Release~",
                table: "UploadCompletionActionExecutions",
                column: "ReleaseCollectionId",
                principalTable: "ReleaseCollections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UploadCompletionActionExecutions_ReleaseCollections_Release~",
                table: "UploadCompletionActionExecutions"
            );

            migrationBuilder.DropIndex(
                name: "IX_UploadCompletionActionExecutions_ReleaseCollectionId_Upload~",
                table: "UploadCompletionActionExecutions"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_UploadCompletionActionExecution_Owner",
                table: "UploadCompletionActionExecutions"
            );

            migrationBuilder.DropColumn(
                name: "ReleaseCollectionId",
                table: "UploadCompletionActionExecutions"
            );

            migrationBuilder.AlterColumn<int>(
                name: "ReleaseId",
                table: "UploadCompletionActionExecutions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true
            );
        }
    }
}
