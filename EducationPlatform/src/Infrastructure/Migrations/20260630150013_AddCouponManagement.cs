using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCouponManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "StudentID",
                table: "Coupons",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "CurrentUsage",
                table: "Coupons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Coupons",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiredDate",
                table: "Coupons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Coupons",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsage",
                table: "Coupons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Coupons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Coupons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Coupons",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Coupons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Data migration for existing coupons
            migrationBuilder.Sql("UPDATE \"Coupons\" SET \"Description\" = \"Reason\";");
            migrationBuilder.Sql("UPDATE \"Coupons\" SET \"StartDate\" = \"CreatedAt\";");
            migrationBuilder.Sql("UPDATE \"Coupons\" SET \"ExpiredDate\" = \"CreatedAt\" + interval '1 year';");
            migrationBuilder.Sql("UPDATE \"Coupons\" SET \"MaxUsage\" = 1;");
            migrationBuilder.Sql("UPDATE \"Coupons\" SET \"CurrentUsage\" = CASE WHEN \"IsUsed\" = true THEN 1 ELSE 0 END;");
            migrationBuilder.Sql("UPDATE \"Coupons\" SET \"IsActive\" = true;");
            migrationBuilder.Sql("UPDATE \"Coupons\" SET \"Type\" = 1;");
            migrationBuilder.Sql("UPDATE \"Coupons\" SET \"Version\" = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentUsage",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "ExpiredDate",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "MaxUsage",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Coupons");

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentID",
                table: "Coupons",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
