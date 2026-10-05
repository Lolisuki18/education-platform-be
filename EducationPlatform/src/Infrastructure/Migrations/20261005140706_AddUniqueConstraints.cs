using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing data may already break the new rules (the old code only checked them in memory).
            // Keep the oldest row of every duplicate group and repair the rest, so the unique indexes can be built.

            // Slugs were free text (up to 4000 chars, "Default" when missing): shorten, then make duplicates unique
            migrationBuilder.Sql(@"
                UPDATE ""Courses"" SET ""Slug"" = LEFT(""Slug"", 190) WHERE LENGTH(""Slug"") > 190;

                UPDATE ""Courses"" c
                SET ""Slug"" = LEFT(c.""Slug"", 190) || '-' || LEFT(REPLACE(c.""CourseID""::text, '-', ''), 8)
                FROM (
                    SELECT ""CourseID"",
                           ROW_NUMBER() OVER (PARTITION BY ""Slug"" ORDER BY ""CreatedAt"", ""CourseID"") AS rn
                    FROM ""Courses""
                ) d
                WHERE c.""CourseID"" = d.""CourseID"" AND d.rn > 1;");

            // A student enrolled twice in a course: keep the first enrollment (its progress cascades away with the others)
            migrationBuilder.Sql(@"
                DELETE FROM ""Enrollments""
                WHERE ""EnrollmentID"" IN (
                    SELECT ""EnrollmentID"" FROM (
                        SELECT ""EnrollmentID"",
                               ROW_NUMBER() OVER (PARTITION BY ""StudentID"", ""CourseID"" ORDER BY ""EnrolledAt"", ""EnrollmentID"") AS rn
                        FROM ""Enrollments""
                    ) ranked
                    WHERE rn > 1
                );");

            // A student reviewed a course twice: keep the first review
            migrationBuilder.Sql(@"
                DELETE FROM ""CourseReviews""
                WHERE ""Id"" IN (
                    SELECT ""Id"" FROM (
                        SELECT ""Id"",
                               ROW_NUMBER() OVER (PARTITION BY ""StudentID"", ""CourseID"" ORDER BY ""CreatedAt"", ""Id"") AS rn
                        FROM ""CourseReviews""
                    ) ranked
                    WHERE rn > 1
                );");

            migrationBuilder.DropIndex(
                name: "IX_CourseReviews_StudentID",
                table: "CourseReviews");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Courses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentID_CourseID",
                table: "Enrollments",
                columns: new[] { "StudentID", "CourseID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Slug",
                table: "Courses",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseReviews_StudentID_CourseID",
                table: "CourseReviews",
                columns: new[] { "StudentID", "CourseID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_StudentID_CourseID",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Courses_Slug",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_CourseReviews_StudentID_CourseID",
                table: "CourseReviews");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Courses",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_CourseReviews_StudentID",
                table: "CourseReviews",
                column: "StudentID");
        }
    }
}
