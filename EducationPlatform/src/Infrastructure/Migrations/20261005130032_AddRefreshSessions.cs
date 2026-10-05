using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefreshSessions",
                columns: table => new
                {
                    SessionID = table.Column<Guid>(type: "uuid", nullable: false),
                    UserID = table.Column<Guid>(type: "uuid", nullable: false),
                    Hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshSessions", x => x.SessionID);
                    table.ForeignKey(
                        name: "FK_RefreshSessions_Users_UserID",
                        column: x => x.UserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_Hash",
                table: "RefreshSessions",
                column: "Hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_UserID",
                table: "RefreshSessions",
                column: "UserID");

            // Keep users signed in: carry every existing single refresh token over as a session.
            migrationBuilder.Sql(@"
                INSERT INTO ""RefreshSessions"" (""SessionID"", ""UserID"", ""Hash"", ""CreatedAt"", ""ExpiresAt"", ""RevokedAt"")
                SELECT gen_random_uuid(), ""UserID"", ""RefreshTokenHash"", now(), ""RefreshTokenExpiresAt"", NULL
                FROM ""Users""
                WHERE ""RefreshTokenHash"" IS NOT NULL AND ""RefreshTokenExpiresAt"" IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenHash",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshSessions");

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshTokenHash",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
