using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// E-mail addresses are now stored trimmed and in lower case, and looked up the same way: "Alice@Mail.com"
    /// and "alice@mail.com" are one account. This brings the existing rows in line. A row is left alone when
    /// another account already owns its normalized form, so the unique index can never be violated.
    /// </summary>
    [DbContext(typeof(EducationPlatformDBContext))]
    [Migration("20261007120000_NormalizeUserEmails")]
    public partial class NormalizeUserEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Users" u
                SET "Email" = lower(btrim(u."Email"))
                WHERE u."Email" <> lower(btrim(u."Email"))
                  AND NOT EXISTS (
                      SELECT 1 FROM "Users" o
                      WHERE o."UserID" <> u."UserID"
                        AND lower(btrim(o."Email")) = lower(btrim(u."Email")));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original casing is not kept, and the lower-case form is just as valid an address
        }
    }
}
