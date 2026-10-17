using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bearcat.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddVideoFileUploadSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ArchiveFileId",
                table: "UploadedFiles",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER"
            );

            migrationBuilder.AddColumn<string>(
                name: "HosterEmbedLink",
                table: "UploadedFiles",
                type: "TEXT",
                maxLength: 500,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "VideoFileRelativePath",
                table: "UploadedFiles",
                type: "TEXT",
                maxLength: 1000,
                nullable: true
            );

            migrationBuilder.AlterColumn<int>(
                name: "ArchiveConfigTemplateId",
                table: "UploadConfigTemplates",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER"
            );

            migrationBuilder.AddColumn<int>(
                name: "UploadSource",
                table: "UploadConfigTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AlterColumn<int>(
                name: "ArchiveConfigId",
                table: "UploadConfigs",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER"
            );

            migrationBuilder.AddColumn<int>(
                name: "UploadSource",
                table: "UploadConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_UploadedFile_FileSource",
                table: "UploadedFiles",
                sql: "(\"ArchiveFileId\" IS NULL) <> (\"VideoFileRelativePath\" IS NULL)"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_UploadConfigTemplate_UploadSource",
                table: "UploadConfigTemplates",
                sql: "(\"UploadSource\" = 1) = (\"ArchiveConfigTemplateId\" IS NOT NULL)"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_UploadConfig_UploadSource",
                table: "UploadConfigs",
                sql: "(\"UploadSource\" = 1) = (\"ArchiveConfigId\" IS NOT NULL)"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UploadedFile_FileSource",
                table: "UploadedFiles"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_UploadConfigTemplate_UploadSource",
                table: "UploadConfigTemplates"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_UploadConfig_UploadSource",
                table: "UploadConfigs"
            );

            migrationBuilder.DropColumn(name: "HosterEmbedLink", table: "UploadedFiles");

            migrationBuilder.DropColumn(name: "VideoFileRelativePath", table: "UploadedFiles");

            migrationBuilder.DropColumn(name: "UploadSource", table: "UploadConfigTemplates");

            migrationBuilder.DropColumn(name: "UploadSource", table: "UploadConfigs");

            migrationBuilder.AlterColumn<int>(
                name: "ArchiveFileId",
                table: "UploadedFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<int>(
                name: "ArchiveConfigTemplateId",
                table: "UploadConfigTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true
            );

            migrationBuilder.AlterColumn<int>(
                name: "ArchiveConfigId",
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
