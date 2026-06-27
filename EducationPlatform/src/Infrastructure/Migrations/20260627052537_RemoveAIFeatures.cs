using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAIFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AISubmissions");

            migrationBuilder.DropTable(
                name: "AIAssignments");

            migrationBuilder.DropTable(
                name: "AIImprovementSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIImprovementSessions",
                columns: table => new
                {
                    SessionID = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CourseID = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnrollmentID = table.Column<Guid>(type: "uuid", nullable: false),
                    Insight = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StudentID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIImprovementSessions", x => x.SessionID);
                });

            migrationBuilder.CreateTable(
                name: "AIAssignments",
                columns: table => new
                {
                    AIAssignmentID = table.Column<Guid>(type: "uuid", nullable: false),
                    Guidance = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LessonID = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SessionID = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIAssignments", x => x.AIAssignmentID);
                    table.ForeignKey(
                        name: "FK_AIAssignments_AIImprovementSessions_SessionID",
                        column: x => x.SessionID,
                        principalTable: "AIImprovementSessions",
                        principalColumn: "SessionID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AISubmissions",
                columns: table => new
                {
                    AISubmissionID = table.Column<Guid>(type: "uuid", nullable: false),
                    AIAssignmentID = table.Column<Guid>(type: "uuid", nullable: false),
                    Answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AISubmissions", x => x.AISubmissionID);
                    table.ForeignKey(
                        name: "FK_AISubmissions_AIAssignments_AIAssignmentID",
                        column: x => x.AIAssignmentID,
                        principalTable: "AIAssignments",
                        principalColumn: "AIAssignmentID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIAssignments_SessionID",
                table: "AIAssignments",
                column: "SessionID");

            migrationBuilder.CreateIndex(
                name: "IX_AISubmissions_AIAssignmentID",
                table: "AISubmissions",
                column: "AIAssignmentID",
                unique: true);
        }
    }
}
