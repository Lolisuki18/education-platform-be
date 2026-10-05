using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseTitleSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Course search is a case-insensitive "contains" (lower(Title) LIKE '%term%'). A trigram index makes
            // that fast instead of scanning every course. The extension needs the right to create it; when the
            // database user lacks it the migration still succeeds and search simply stays unindexed.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    CREATE EXTENSION IF NOT EXISTS pg_trgm;
                    CREATE INDEX IF NOT EXISTS ""IX_Courses_Title_trgm"" ON ""Courses"" USING gin (lower(""Title"") gin_trgm_ops);
                EXCEPTION WHEN OTHERS THEN
                    RAISE NOTICE 'Skipped the course title search index: %', SQLERRM;
                END
                $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Courses_Title_trgm"";");
        }
    }
}
