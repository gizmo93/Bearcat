using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BearCat.Core.Migrations;

/// <inheritdoc />
public partial class ApplyPendingChanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateTime>(
            name: "UploadedAt",
            table: "Uploads",
            type: "timestamp(4) without time zone",
            precision: 4,
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) with time zone",
            oldPrecision: 4,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "Uploads",
            type: "timestamp(4) without time zone",
            precision: 4,
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) with time zone",
            oldPrecision: 4);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "UploadedFiles",
            type: "timestamp(4) without time zone",
            precision: 4,
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) with time zone",
            oldPrecision: 4);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CheckedAt",
            table: "UploadedFiles",
            type: "timestamp(4) without time zone",
            precision: 4,
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) with time zone",
            oldPrecision: 4,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "ResolvedAt",
            table: "Notifications",
            type: "timestamp(4) without time zone",
            precision: 4,
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) with time zone",
            oldPrecision: 4,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "Notifications",
            type: "timestamp(4) without time zone",
            precision: 4,
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) with time zone",
            oldPrecision: 4);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "Archives",
            type: "timestamp(4) without time zone",
            precision: 4,
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) with time zone",
            oldPrecision: 4);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<DateTime>(
            name: "UploadedAt",
            table: "Uploads",
            type: "timestamp(4) with time zone",
            precision: 4,
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) without time zone",
            oldPrecision: 4,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "Uploads",
            type: "timestamp(4) with time zone",
            precision: 4,
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) without time zone",
            oldPrecision: 4);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "UploadedFiles",
            type: "timestamp(4) with time zone",
            precision: 4,
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) without time zone",
            oldPrecision: 4);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CheckedAt",
            table: "UploadedFiles",
            type: "timestamp(4) with time zone",
            precision: 4,
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) without time zone",
            oldPrecision: 4,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "ResolvedAt",
            table: "Notifications",
            type: "timestamp(4) with time zone",
            precision: 4,
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) without time zone",
            oldPrecision: 4,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "Notifications",
            type: "timestamp(4) with time zone",
            precision: 4,
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) without time zone",
            oldPrecision: 4);

        migrationBuilder.AlterColumn<DateTime>(
            name: "CreatedAt",
            table: "Archives",
            type: "timestamp(4) with time zone",
            precision: 4,
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "timestamp(4) without time zone",
            oldPrecision: 4);
    }
}
