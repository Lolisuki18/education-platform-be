using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using Application.Features.StudentReview.Command;
using Application.Results;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Controllers
{
    public class ReviewsControllerTests : IntegrationTestBase
    {
        public ReviewsControllerTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task CreateReview_Success_And_DuplicateReview_ReturnsConflict()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Course? course = null;
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");

                // Student must be enrolled in the course to review it
                var enrollment = new Enrollment(Guid.NewGuid(), student.UserID, course.CourseID, null);
                db.Set<Enrollment>().Add(enrollment);
                await db.SaveChangesAsync();
            });
            course.Should().NotBeNull();

            var command = new CreateReviewCommand
            {
                Rating = 4.5f,
                Comment = "Excellent course!"
            };

            // Act 1: Submit first review
            var response1 = await Client.PostAsJsonAsync($"/api/courses/{course!.CourseID}/reviews", command);

            // Assert 1: First review succeeds
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            var result1 = await response1.Content.ReadFromJsonAsync<ApiResponse<CourseReviewDTO>>();
            result1.Should().NotBeNull();
            result1!.IsSuccess.Should().BeTrue();
            result1.Data!.Rating.Should().Be(4.5f);
            result1.Data.Comment.Should().Be("Excellent course!");

            // Act 2: Attempt to submit duplicate review
            var response2 = await Client.PostAsJsonAsync($"/api/courses/{course.CourseID}/reviews", command);

            // Assert 2: Fails with 409 Conflict
            response2.StatusCode.Should().Be(HttpStatusCode.Conflict);

            // Act 3: Get reviews for the course
            var getResponse = await Client.GetAsync($"/api/courses/{course.CourseID}/reviews?pageIndex=1&pageSize=10");

            // Assert 3: Return list containing the review
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var getResult = await getResponse.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<CourseReviewDTO>>>();
            getResult.Should().NotBeNull();
            getResult!.IsSuccess.Should().BeTrue();
            getResult.Data.Should().ContainSingle(r => r.Comment == "Excellent course!");
        }
    }
}
