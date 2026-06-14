using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionImageUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ReleaseId",
                table: "ImageUploadConfigs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer"
            );

            migrationBuilder.AddColumn<int>(
                name: "ReleaseCollectionId",
                table: "ImageUploadConfigs",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "CollectionImageUploadConfigTemplates",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseTemplateId = table.Column<int>(type: "integer", nullable: false),
                    ImageHosterRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionImageUploadConfigTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionImageUploadConfigTemplates_ImageHosterRegistratio~",
                        column: x => x.ImageHosterRegistrationId,
                        principalTable: "ImageHosterRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_CollectionImageUploadConfigTemplates_ReleaseTemplates_Relea~",
                        column: x => x.ReleaseTemplateId,
                        principalTable: "ReleaseTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ImageUploadConfigs_ReleaseCollectionId",
                table: "ImageUploadConfigs",
                column: "ReleaseCollectionId"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_ImageUploadConfig_Owner",
                table: "ImageUploadConfigs",
                sql: "(\"ReleaseId\" IS NOT NULL) <> (\"ReleaseCollectionId\" IS NOT NULL)"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CollectionImageUploadConfigTemplates_ImageHosterRegistratio~",
                table: "CollectionImageUploadConfigTemplates",
                column: "ImageHosterRegistrationId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CollectionImageUploadConfigTemplates_ReleaseTemplateId",
                table: "CollectionImageUploadConfigTemplates",
                column: "ReleaseTemplateId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ImageUploadConfigs_ReleaseCollections_ReleaseCollectionId",
                table: "ImageUploadConfigs",
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
                name: "FK_ImageUploadConfigs_ReleaseCollections_ReleaseCollectionId",
                table: "ImageUploadConfigs"
            );

            migrationBuilder.DropTable(name: "CollectionImageUploadConfigTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ImageUploadConfigs_ReleaseCollectionId",
                table: "ImageUploadConfigs"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_ImageUploadConfig_Owner",
                table: "ImageUploadConfigs"
            );

            migrationBuilder.DropColumn(name: "ReleaseCollectionId", table: "ImageUploadConfigs");

            migrationBuilder.AlterColumn<int>(
                name: "ReleaseId",
                table: "ImageUploadConfigs",
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
