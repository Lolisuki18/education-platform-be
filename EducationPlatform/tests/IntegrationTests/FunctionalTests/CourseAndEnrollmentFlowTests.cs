using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using API.Models.Courses;
using API.Models.Orders;
using Application.Features.Enrollments.Commands;
using Application.Results;
using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Enum;
using Domain.CourseManagement.Entity;
using Domain.EnrollmentManagement.Aggregate;
using Domain.EnrollmentManagement.Entity;
using Domain.EnrollmentManagement.Enum;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using FluentAssertions;
using Infrastructure.Persistence;
using IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FunctionalTests
{
    public class CourseAndEnrollmentFlowTests : IntegrationTestBase
    {
        public CourseAndEnrollmentFlowTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task PublishAndEnrollCourseFlow_ShouldSucceed()
        {
            // 1. Create authenticated clients for roles
            var teacherClient = await CreateAuthenticatedClientAsync("teacher@example.com", "Password123!");
            var adminClient = await CreateAuthenticatedClientAsync("admin@example.com", "Password123!");
            var studentClient = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");

            // 2. Fetch Grade and Subject IDs
            Guid gradeId = Guid.Empty;
            Guid subjectId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                var grade = await db.Set<Grade>().FirstAsync();
                var subject = await db.Set<Subject>().FirstAsync();
                gradeId = grade.GradeID;
                subjectId = subject.SubjectID;
            });

            // 3. Teacher creates a course
            var fields = new Dictionary<string, string>
            {
                { "Title", "E2E Course" },
                { "Description", "E2E Course Description" },
                { "Price", "12000" },
                { "ThumbnailName", "e2e.jpg" },
                { "Slug", "e2e-course" },
                { "Prerequisites", "None" },
                { "LearningOutcomes", "Learn E2E" },
                { "GradeID", gradeId.ToString() },
                { "SubjectID", subjectId.ToString() },
                { "Chapters[0].Title", "Chapter 1: Intro" },
                { "Chapters[0].Description", "Intro chapter" },
                { "Chapters[0].Order", "1" },
                { "Chapters[0].Lessons[0].Title", "Lesson 1.1: Hello" },
                { "Chapters[0].Lessons[0].Objectives", "Hello world" },
                { "Chapters[0].Lessons[0].Description", "First lesson" },
                { "Chapters[0].Lessons[0].VideoUrl", "https://example.com/hello-video" },
                { "Chapters[0].Lessons[0].Order", "1" }
            };
            var content = CreateMultipartFormContent(fields, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x00, 0x00, 0x00 }, "ThumbnailFile", "e2e.jpg", "image/jpeg");
            var createResponse = await teacherClient.PostAsync("/api/courses", content);
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
            var courseId = createResult!.Data;
            courseId.Should().NotBeEmpty();

            // 4. Admin reviews and publishes the course
            var reviewPayload = new
            {
                CourseID = courseId,
                IsApproved = true,
                AdminNote = "Looks excellent!"
            };
            var reviewResponse = await adminClient.PostAsJsonAsync("/api/courses/review", reviewPayload);
            reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify published status
            await ExecuteDbContextAsync(async db =>
            {
                var course = await db.Set<Course>().FindAsync(courseId);
                course!.Status.Should().Be(CourseStatus.Published);
            });

            // 5. Student creates an Order for the published course
            var orderResponse = await studentClient.PostAsJsonAsync("/api/orders", new CreateOrderRequestDto
            {
                CourseId = courseId,
                SelectedCouponIds = new List<Guid>() // No coupon
            });
            orderResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var orderResult = await orderResponse.Content.ReadFromJsonAsync<ApiResponse<CreateOrderResponseDto>>();
            orderResult.Should().NotBeNull();

            // Fetch created order from db to get OrderCode
            long orderCode = 0;
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var order = await db.Set<Order>().FirstOrDefaultAsync(o => o.StudentID == student.UserID && o.CourseID == courseId);
                order.Should().NotBeNull();
                orderCode = order.OrderCode;
            });

            // 6. Complete payment by simulating the callback
            var paymentReturnResponse = await studentClient.GetAsync($"/api/orders/return?status=PAID&orderCode={orderCode}");
            paymentReturnResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify enrollment is created
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var enrollment = await db.Set<Enrollment>().FirstOrDefaultAsync(e => e.StudentID == student.UserID && e.CourseID == courseId);
                enrollment.Should().NotBeNull();
                enrollment!.Status.Should().Be(EnrollmentStatus.Active);
            });
        }

        [Fact]
        public async Task PurchaseCourseWithCouponAndCompletePaymentFlow_ShouldSucceed()
        {
            var studentClient = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");

            // 1. Fetch Student, Course and Coupon details
            Guid studentId = Guid.Empty;
            Guid courseId = Guid.Empty;
            Guid couponId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                studentId = student.UserID;

                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra" && c.Status == CourseStatus.Published);
                courseId = course.CourseID;

                var coupon = await db.Set<Coupon>().FirstAsync(c => c.Code == "DISCOUNT10");
                couponId = coupon.CouponID;
            });

            // 2. Student creates an Order with Coupon
            var orderResponse = await studentClient.PostAsJsonAsync("/api/orders", new CreateOrderRequestDto
            {
                CourseId = courseId,
                SelectedCouponIds = new List<Guid> { couponId }
            });
            orderResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var orderResult = await orderResponse.Content.ReadFromJsonAsync<ApiResponse<CreateOrderResponseDto>>();
            orderResult.Should().NotBeNull();
            orderResult!.Data.CheckoutUrl.Should().Be("https://mock-payment-url.com");

            // Fetch created order from db to get OrderCode
            long orderCode = 0;
            await ExecuteDbContextAsync(async db =>
            {
                var order = await db.Set<Order>().FirstOrDefaultAsync(o => o.StudentID == studentId && o.CourseID == courseId);
                order.Should().NotBeNull();
                order!.Status.Should().Be(OrderStatus.Created);
                orderCode = order.OrderCode;
            });

            // 3. Complete payment by simulating the callback
            var paymentReturnResponse = await studentClient.GetAsync($"/api/orders/return?status=PAID&orderCode={orderCode}");
            paymentReturnResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Assert Order is Completed and Enrollment is automatically created
            await ExecuteDbContextAsync(async db =>
            {
                var order = await db.Set<Order>().FirstOrDefaultAsync(o => o.StudentID == studentId && o.CourseID == courseId);
                order!.Status.Should().Be(OrderStatus.Pending);

                var enrollment = await db.Set<Enrollment>().FirstOrDefaultAsync(e => e.StudentID == studentId && e.CourseID == courseId);
                enrollment.Should().NotBeNull();
                enrollment!.Status.Should().Be(EnrollmentStatus.Active);
            });
        }

        [Fact]
        public async Task UpdateLearningProgressToCompletionFlow_ShouldSucceed()
        {
            var studentClient = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");

            Guid studentId = Guid.Empty;
            Guid courseId = Guid.Empty;
            Guid enrollmentId = Guid.Empty;
            Guid chapterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            Guid lessonId = Guid.Parse("22222222-2222-2222-2222-222222222222");

            // 1. Setup: Enroll the student in Course 1 ("Math algebra")
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                studentId = student.UserID;

                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");
                courseId = course.CourseID;

                // Enroll the student
                var enrollment = new Enrollment(Guid.NewGuid(), studentId, courseId, null);
                db.Set<Enrollment>().Add(enrollment);

                // Add empty course progress
                var progress = new CourseProgress(Guid.NewGuid(), enrollment.EnrollmentID);
                db.Set<CourseProgress>().Add(progress);

                await db.SaveChangesAsync();
                enrollmentId = enrollment.EnrollmentID;
            });

            // 2. Student calls the API to mark the lesson as completed
            var progressResponse = await studentClient.PostAsJsonAsync("/api/enrollments/progress/lesson", new
            {
                EnrollmentID = enrollmentId,
                ChapterID = chapterId,
                LessonID = lessonId,
                IsCompleted = true
            });
            progressResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Assert that progress was updated to 100% and marked completed
            await ExecuteDbContextAsync(async db =>
            {
                var progress = await db.Set<CourseProgress>().FirstOrDefaultAsync(p => p.EnrollmentID == enrollmentId);
                progress.Should().NotBeNull();
                progress!.CompletionRate.Should().Be(100);
                progress.IsCompleted.Should().BeTrue();
            });
        }

        [Fact]
        public async Task SubmitQuizAndCompleteEnrollmentFlow_ShouldSucceed()
        {
            var studentClient = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");

            Guid studentId = Guid.Empty;
            Guid courseId = Guid.Empty;
            Guid enrollmentId = Guid.Empty;
            Guid chapterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            Guid lessonId = Guid.Parse("22222222-2222-2222-2222-222222222222");
            Guid quizId = Guid.NewGuid();

            // 1. Setup: Seed Quiz & QuizAnswer, and enroll the student in Course 1 ("Math algebra")
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                studentId = student.UserID;

                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");
                courseId = course.CourseID;

                // Add Quiz to the Lesson
                var quiz = new Quiz(quizId, "What is 1 + 1?", "Simple math", lessonId);
                quiz.AddAnswer(QuizType.SingleChoice, new List<string> { "2" }, new List<string> { "1", "2", "3" });
                db.Set<Quiz>().Add(quiz);

                // Enroll the student
                var enrollment = new Enrollment(Guid.NewGuid(), studentId, courseId, null);
                db.Set<Enrollment>().Add(enrollment);

                // Add empty course progress
                var progress = new CourseProgress(Guid.NewGuid(), enrollment.EnrollmentID);
                db.Set<CourseProgress>().Add(progress);

                await db.SaveChangesAsync();
                enrollmentId = enrollment.EnrollmentID;
            });

            // 2. Submit INCORRECT answers first (fails the quiz)
            var quizPayloadIncorrect = new
            {
                EnrollmentID = enrollmentId,
                ChapterID = chapterId,
                LessonID = lessonId,
                QuizID = quizId,
                SelectedAnswers = new List<string> { "3" }
            };
            var quizResponse1 = await studentClient.PostAsJsonAsync("/api/enrollments/progress/quiz", quizPayloadIncorrect);
            quizResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
            var quizResult1 = await quizResponse1.Content.ReadFromJsonAsync<ApiResponse<SubmitQuizResult>>();
            quizResult1.Should().NotBeNull();
            quizResult1!.Data!.IsCorrect.Should().BeFalse();

            // Verify progress is NOT complete in the database
            await ExecuteDbContextAsync(async db =>
            {
                var progress = await db.Set<CourseProgress>()
                    .Include(cp => cp.ChapterProgresses)
                        .ThenInclude(chp => chp.LessonProgresses)
                    .FirstOrDefaultAsync(p => p.EnrollmentID == enrollmentId);
                progress.Should().NotBeNull();
                progress!.IsCompleted.Should().BeFalse();
                progress.CompletionRate.Should().Be(0);
            });

            // 3. Submit CORRECT answers next (passes the quiz)
            var quizPayloadCorrect = new
            {
                EnrollmentID = enrollmentId,
                ChapterID = chapterId,
                LessonID = lessonId,
                QuizID = quizId,
                SelectedAnswers = new List<string> { "2" }
            };
            var quizResponse2 = await studentClient.PostAsJsonAsync("/api/enrollments/progress/quiz", quizPayloadCorrect);
            quizResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
            var quizResult2 = await quizResponse2.Content.ReadFromJsonAsync<ApiResponse<SubmitQuizResult>>();
            quizResult2!.Data!.IsCorrect.Should().BeTrue();
            quizResult2.Data.Explanation.Should().Be("Simple math");

            // Verify progress is now updated to completed
            await ExecuteDbContextAsync(async db =>
            {
                var progress = await db.Set<CourseProgress>()
                    .Include(cp => cp.ChapterProgresses)
                        .ThenInclude(chp => chp.LessonProgresses)
                    .FirstOrDefaultAsync(p => p.EnrollmentID == enrollmentId);
                progress.Should().NotBeNull();
                progress!.IsCompleted.Should().BeTrue();
                progress.CompletionRate.Should().Be(100);

                var enrollment = await db.Set<Enrollment>().FindAsync(enrollmentId);
                enrollment!.CompletedAt.Should().NotBeNull();
            });
        }
    }
}
