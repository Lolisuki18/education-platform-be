using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDeletedAtAndDropDuplicateSubjectKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DefaultLessons_Subjects_SubjectID1",
                table: "DefaultLessons");

            migrationBuilder.DropIndex(
                name: "IX_DefaultLessons_SubjectID1",
                table: "DefaultLessons");

            migrationBuilder.DropColumn(
                name: "SubjectID1",
                table: "DefaultLessons");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectID1",
                table: "DefaultLessons",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DefaultLessons_SubjectID1",
                table: "DefaultLessons",
                column: "SubjectID1");

            migrationBuilder.AddForeignKey(
                name: "FK_DefaultLessons_Subjects_SubjectID1",
                table: "DefaultLessons",
                column: "SubjectID1",
                principalTable: "Subjects",
                principalColumn: "SubjectID");
        }
    }
}
