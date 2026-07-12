using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using API.Models.Academic;
using Application.Results;
using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Collections.Generic;

namespace IntegrationTests.Controllers
{
    public class AcademicControllerTests : IntegrationTestBase
    {
        public AcademicControllerTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task GetGrades_PublicAccess_ReturnsActiveGrades()
        {
            // Act
            var response = await Client.GetAsync("/api/academic/grades");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<GradeDTO>>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
        }

        [Fact]
        public async Task CreateGrade_AsStudent_ReturnsForbidden()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            var request = new CreateGradeRequestDto { Name = "New Grade" };

            // Act
            var response = await Client.PostAsJsonAsync("/api/academic/grades", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task CreateGrade_AsAdmin_CreatesGrade()
        {
            // Arrange
            await LoginExistingUserAsync("admin@example.com", "Password123!");

            var request = new CreateGradeRequestDto { Name = "New Grade Admin" };

            // Act
            var response = await Client.PostAsJsonAsync("/api/academic/grades", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeEmpty();
        }

        [Fact]
        public async Task DeactivateGrade_InUse_ReturnsConflict()
        {
            // Arrange
            await LoginExistingUserAsync("admin@example.com", "Password123!");

            // Seed a course that will reference a grade
            Guid gradeId;
            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
                var g = new Grade(Guid.NewGuid(), "In Use Grade");
                db.Set<Grade>().Add(g);
                gradeId = g.GradeID;

                var teacher = await db.Set<Domain.IdentityManagement.Aggregate.User>().FirstOrDefaultAsync(u => u.Role == Domain.IdentityManagement.Enum.Role.Teacher);
                var subject = await db.Set<Subject>().FirstOrDefaultAsync();

                var course = new Course(
                    Guid.NewGuid(),
                    "In Use Course",
                    "Description",
                    null,
                    "thumb.jpg",
                    "in-use",
                    "Prereq",
                    "Outcome",
                    teacher!.UserID,
                    g.GradeID,
                    subject!.SubjectID,
                    DateTime.UtcNow
                );
                db.Set<Course>().Add(course);
                await db.SaveChangesAsync();
            }

            // Act
            var response = await Client.DeleteAsync($"/api/academic/grades/{gradeId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
    }
}
