using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using Application.Features.StudentReview.Command;
using Application.Results;
using Domain.CourseManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using FluentAssertions;
using IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FunctionalTests
{
    public class ReviewFlowTests : IntegrationTestBase
    {
        public ReviewFlowTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task CourseReviewAndRatingFlow_ShouldSucceed()
        {
            var studentClient = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");

            Guid studentId = Guid.Empty;
            Guid courseId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                studentId = student.UserID;

                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");
                courseId = course.CourseID;

                // Enroll the student first to satisfy the review policy
                var enrollment = new Enrollment(Guid.NewGuid(), studentId, courseId, null);
                db.Set<Enrollment>().Add(enrollment);
                await db.SaveChangesAsync();
            });

            // 1. Student submits a review
            var reviewCommand = new CreateReviewCommand
            {
                Rating = 4.5f,
                Comment = "Really great explanations!"
            };
            var reviewResponse = await studentClient.PostAsJsonAsync($"/api/courses/{courseId}/reviews", reviewCommand);
            reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var reviewResult = await reviewResponse.Content.ReadFromJsonAsync<ApiResponse<CourseReviewDTO>>();
            reviewResult.Should().NotBeNull();
            reviewResult!.IsSuccess.Should().BeTrue();
            reviewResult.Data!.Rating.Should().Be(4.5f);
            reviewResult.Data.Comment.Should().Be("Really great explanations!");

            // 2. Fetch the reviews list and verify our review is present
            var getReviewsResponse = await studentClient.GetAsync($"/api/courses/{courseId}/reviews");
            getReviewsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var getReviewsResult = await getReviewsResponse.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<CourseReviewDTO>>>();
            getReviewsResult.Should().NotBeNull();
            getReviewsResult!.Data.Should().Contain(r => r.Comment == "Really great explanations!" && r.Rating == 4.5f);

            // 3. Attempt to submit review again (Duplicate check - Idempotency)
            var duplicateResponse = await studentClient.PostAsJsonAsync($"/api/courses/{courseId}/reviews", reviewCommand);
            duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
    }
}
