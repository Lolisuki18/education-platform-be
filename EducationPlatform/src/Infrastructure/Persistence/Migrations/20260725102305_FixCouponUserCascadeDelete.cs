using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixCouponUserCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Coupons_Users_StudentID",
                table: "Coupons");

            // NOTE: "xmin" is a PostgreSQL system column that already exists on every table.
            // It is mapped as a shadow concurrency-token property (see EducationPlatformDBContext),
            // not a real column to create/drop, so no AddColumn/DropColumn is emitted for it here.

            migrationBuilder.AddForeignKey(
                name: "FK_Coupons_Users_StudentID",
                table: "Coupons",
                column: "StudentID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Coupons_Users_StudentID",
                table: "Coupons");

            migrationBuilder.AddForeignKey(
                name: "FK_Coupons_Users_StudentID",
                table: "Coupons",
                column: "StudentID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
