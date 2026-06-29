using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using Application.Features.Enrollments.Commands;
using Application.Features.Enrollments.Queries.GetEnrollmentDetail;
using Application.Results;
using Domain.CourseManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.EnrollmentManagement.Entity;
using Domain.IdentityManagement.Aggregate;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Controllers
{
    public class EnrollmentsControllerTests : IntegrationTestBase
    {
        public EnrollmentsControllerTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task ListEnrollments_ReturnsEnrolledCourses()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Guid enrollmentId = Guid.NewGuid();
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");

                var enrollment = new Enrollment(enrollmentId, student.UserID, course.CourseID, null);
                db.Set<Enrollment>().Add(enrollment);
                await db.SaveChangesAsync();
            });

            // Act
            var response = await Client.GetAsync("/api/enrollments");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<EnrollmentDTO>>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().ContainSingle(e => e.EnrollmentID == enrollmentId);
        }

        [Fact]
        public async Task GetEnrollmentDetail_ReturnsDetails()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Guid enrollmentId = Guid.NewGuid();
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");

                var enrollment = new Enrollment(enrollmentId, student.UserID, course.CourseID, null);
                db.Set<Enrollment>().Add(enrollment);
                await db.SaveChangesAsync();
            });

            // Act
            var response = await Client.GetAsync($"/api/enrollments/{enrollmentId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<EnrollmentDetailDTO>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.EnrollmentID.Should().Be(enrollmentId);
        }

        [Fact]
        public async Task UpdateLessonProgress_UpdatesSuccessfully()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Guid enrollmentId = Guid.NewGuid();
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");

                var enrollment = new Enrollment(enrollmentId, student.UserID, course.CourseID, null);
                db.Set<Enrollment>().Add(enrollment);
                await db.SaveChangesAsync();
            });

            var command = new UpdateLessonProgressCommand
            {
                EnrollmentID = enrollmentId,
                ChapterID = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                LessonID = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                IsCompleted = true
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/enrollments/progress/lesson", command);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();

            // Verify progress is added in the database
            await ExecuteDbContextAsync(async db =>
            {
                var progress = await db.Set<CourseProgress>()
                    .Include(cp => cp.ChapterProgresses)
                        .ThenInclude(ch => ch.LessonProgresses)
                    .FirstOrDefaultAsync(cp => cp.EnrollmentID == enrollmentId);

                progress.Should().NotBeNull();
                progress!.ChapterProgresses.Should().ContainSingle(ch => ch.ChapterID == command.ChapterID);
                var chapterProgress = progress.ChapterProgresses.First();
                chapterProgress.LessonProgresses.Should().ContainSingle(l => l.LessonID == command.LessonID && l.IsCompleted);
            });
        }
    }
}
