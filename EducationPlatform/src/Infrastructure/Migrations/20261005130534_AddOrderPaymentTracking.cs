using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPaymentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: "Orders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "CouponIds",
                table: "Orders",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderCode",
                table: "Orders",
                column: "OrderCode");

            // Before the unique index can exist, keep only the newest unpaid order per student and course.
            migrationBuilder.Sql(@"
                UPDATE ""Orders"" SET ""Status"" = 3
                WHERE ""Status"" = 1 AND ""OrderID"" IN (
                    SELECT ""OrderID"" FROM (
                        SELECT ""OrderID"",
                               ROW_NUMBER() OVER (PARTITION BY ""StudentID"", ""CourseID"" ORDER BY ""CreatedAt"" DESC) AS rn
                        FROM ""Orders""
                        WHERE ""Status"" = 1
                    ) ranked
                    WHERE rn > 1
                );");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StudentID_CourseID",
                table: "Orders",
                columns: new[] { "StudentID", "CourseID" },
                unique: true,
                filter: "\"Status\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderCode",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_StudentID_CourseID",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponIds",
                table: "Orders");
        }
    }
}
