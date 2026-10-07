using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Finishing a course used to set only <c>CompletedAt</c> and leave the status at Active, so the admin dashboard
    /// (which counts the status) never saw a completed enrollment. This fixes the rows written that way.
    /// </summary>
    [DbContext(typeof(EducationPlatformDBContext))]
    [Migration("20261007120100_MarkFinishedEnrollmentsCompleted")]
    public partial class MarkFinishedEnrollmentsCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1 = Active, 2 = Completed
            migrationBuilder.Sql("""
                UPDATE "Enrollments" SET "Status" = 2 WHERE "Status" = 1 AND "CompletedAt" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
