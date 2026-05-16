using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCourseReviewRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "CourseReviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseReviews_CourseID",
                table: "CourseReviews",
                column: "CourseID");

            migrationBuilder.CreateIndex(
                name: "IX_CourseReviews_StudentID",
                table: "CourseReviews",
                column: "StudentID");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseReviews_Courses_CourseID",
                table: "CourseReviews",
                column: "CourseID",
                principalTable: "Courses",
                principalColumn: "CourseID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseReviews_Users_StudentID",
                table: "CourseReviews",
                column: "StudentID",
                principalTable: "Users",
                principalColumn: "UserID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseReviews_Courses_CourseID",
                table: "CourseReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_CourseReviews_Users_StudentID",
                table: "CourseReviews");

            migrationBuilder.DropIndex(
                name: "IX_CourseReviews_CourseID",
                table: "CourseReviews");

            migrationBuilder.DropIndex(
                name: "IX_CourseReviews_StudentID",
                table: "CourseReviews");

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "CourseReviews",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
