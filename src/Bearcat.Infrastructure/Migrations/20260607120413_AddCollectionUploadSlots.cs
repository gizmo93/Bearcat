using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bearcat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionUploadSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollectionUploadSlotExpectedArchivePassword",
                table: "UploadConfigTemplates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "CollectionUploadSlotIsRequired",
                table: "UploadConfigTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<string>(
                name: "CollectionUploadSlotKey",
                table: "UploadConfigTemplates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "CollectionUploadSlotName",
                table: "UploadConfigTemplates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "CollectionUploadSlotPasswordPolicy",
                table: "UploadConfigTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "CollectionUploadSlotId",
                table: "UploadConfigs",
                type: "integer",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "CollectionUploadSlots",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    ReleaseCollectionId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    Name = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordPolicy = table.Column<int>(type: "integer", nullable: false),
                    ExpectedArchivePassword = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionUploadSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionUploadSlots_ReleaseCollections_ReleaseCollectionId",
                        column: x => x.ReleaseCollectionId,
                        principalTable: "ReleaseCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_UploadConfigs_CollectionUploadSlotId",
                table: "UploadConfigs",
                column: "CollectionUploadSlotId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CollectionUploadSlots_ReleaseCollectionId_Key",
                table: "CollectionUploadSlots",
                columns: new[] { "ReleaseCollectionId", "Key" },
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_UploadConfigs_CollectionUploadSlots_CollectionUploadSlotId",
                table: "UploadConfigs",
                column: "CollectionUploadSlotId",
                principalTable: "CollectionUploadSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UploadConfigs_CollectionUploadSlots_CollectionUploadSlotId",
                table: "UploadConfigs"
            );

            migrationBuilder.DropTable(name: "CollectionUploadSlots");

            migrationBuilder.DropIndex(
                name: "IX_UploadConfigs_CollectionUploadSlotId",
                table: "UploadConfigs"
            );

            migrationBuilder.DropColumn(
                name: "CollectionUploadSlotExpectedArchivePassword",
                table: "UploadConfigTemplates"
            );

            migrationBuilder.DropColumn(
                name: "CollectionUploadSlotIsRequired",
                table: "UploadConfigTemplates"
            );

            migrationBuilder.DropColumn(
                name: "CollectionUploadSlotKey",
                table: "UploadConfigTemplates"
            );

            migrationBuilder.DropColumn(
                name: "CollectionUploadSlotName",
                table: "UploadConfigTemplates"
            );

            migrationBuilder.DropColumn(
                name: "CollectionUploadSlotPasswordPolicy",
                table: "UploadConfigTemplates"
            );

            migrationBuilder.DropColumn(name: "CollectionUploadSlotId", table: "UploadConfigs");
        }
    }
}
