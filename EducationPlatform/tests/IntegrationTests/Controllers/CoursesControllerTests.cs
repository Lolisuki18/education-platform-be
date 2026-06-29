using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using Application.Features.Courses.Queries.GetLandingPage;
using Application.Results;
using Domain.CourseManagement.Aggregate;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Controllers
{
    public class CoursesControllerTests : IntegrationTestBase
    {
        public CoursesControllerTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task ListCourses_ReturnsPublishedCoursesOnly()
        {
            // Act
            var response = await Client.GetAsync("/api/courses");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LandingPageResult>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();

            // Check courses list
            var courses = result.Data!.Courses.ToList();
            courses.Should().NotBeEmpty();

            // "Math algebra" is published, "Math geometry" is pending, so only "Math algebra" should be returned
            courses.Should().Contain(c => c.Title == "Math algebra");
            courses.Should().NotContain(c => c.Title == "Math geometry");
        }

        [Fact]
        public async Task GetCourseDetail_WithValidId_ReturnsCourse()
        {
            // Arrange
            Guid publishedCourseId;
            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");
                publishedCourseId = course.CourseID;
            }

            // Act
            var response = await Client.GetAsync($"/api/courses/{publishedCourseId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<CourseDetailDTO>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Title.Should().Be("Math algebra");
        }

        [Fact]
        public async Task GetCourseDetail_WithInvalidId_ReturnsNotFound()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            // Act
            var response = await Client.GetAsync($"/api/courses/{nonExistentId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
