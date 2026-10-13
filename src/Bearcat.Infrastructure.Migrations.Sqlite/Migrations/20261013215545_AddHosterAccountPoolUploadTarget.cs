using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddHosterAccountPoolUploadTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "HosterRegistrationId",
                table: "Uploads",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER"
            );

            migrationBuilder.AlterColumn<int>(
                name: "HosterRegistrationId",
                table: "UploadConfigTemplates",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER"
            );

            migrationBuilder.AddColumn<int>(
                name: "HosterAccountPoolId",
                table: "UploadConfigTemplates",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AlterColumn<int>(
                name: "HosterRegistrationId",
                table: "UploadConfigs",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER"
            );

            migrationBuilder.AddColumn<int>(
                name: "HosterAccountPoolId",
                table: "UploadConfigs",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigTemplates_HosterAccountPoolId",
                table: "UploadConfigTemplates",
                column: "HosterAccountPoolId"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_UploadConfigTemplate_UploadTarget",
                table: "UploadConfigTemplates",
                sql: "(\"HosterRegistrationId\" IS NULL) <> (\"HosterAccountPoolId\" IS NULL)"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigs_HosterAccountPoolId",
                table: "UploadConfigs",
                column: "HosterAccountPoolId"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_UploadConfig_UploadTarget",
                table: "UploadConfigs",
                sql: "(\"HosterRegistrationId\" IS NULL) <> (\"HosterAccountPoolId\" IS NULL)"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_UploadConfigs_HosterAccountPools_HosterAccountPoolId",
                table: "UploadConfigs",
                column: "HosterAccountPoolId",
                principalTable: "HosterAccountPools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_UploadConfigTemplates_HosterAccountPools_HosterAccountPoolId",
                table: "UploadConfigTemplates",
                column: "HosterAccountPoolId",
                principalTable: "HosterAccountPools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UploadConfigs_HosterAccountPools_HosterAccountPoolId",
                table: "UploadConfigs"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_UploadConfigTemplates_HosterAccountPools_HosterAccountPoolId",
                table: "UploadConfigTemplates"
            );

            migrationBuilder.DropIndex(
                name: "IX_UploadConfigTemplates_HosterAccountPoolId",
                table: "UploadConfigTemplates"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_UploadConfigTemplate_UploadTarget",
                table: "UploadConfigTemplates"
            );

            migrationBuilder.DropIndex(
                name: "IX_UploadConfigs_HosterAccountPoolId",
                table: "UploadConfigs"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_UploadConfig_UploadTarget",
                table: "UploadConfigs"
            );

            migrationBuilder.DropColumn(
                name: "HosterAccountPoolId",
                table: "UploadConfigTemplates"
            );

            migrationBuilder.DropColumn(name: "HosterAccountPoolId", table: "UploadConfigs");

            migrationBuilder.AlterColumn<int>(
                name: "HosterRegistrationId",
                table: "Uploads",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<int>(
                name: "HosterRegistrationId",
                table: "UploadConfigTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<int>(
                name: "HosterRegistrationId",
                table: "UploadConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true
            );
        }
    }
}
