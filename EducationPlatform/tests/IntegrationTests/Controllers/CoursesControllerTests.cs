using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using API.Models.Courses;
using Application.Features.Courses.Queries.GetLandingPage;
using Application.Results;
using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
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

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CourseDTO>>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();

            // Check courses list
            var courses = result.Data!.Items.ToList();
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

        [Fact]
        public async Task CreateCourse_AsTeacher_ReturnsSuccess()
        {
            // Arrange
            await LoginExistingUserAsync("teacher@example.com", "Password123!");

            Guid gradeId = Guid.Empty;
            Guid subjectId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                var grade = await db.Set<Grade>().FirstAsync();
                var subject = await db.Set<Subject>().FirstAsync();
                gradeId = grade.GradeID;
                subjectId = subject.SubjectID;
            });

            var fields = new Dictionary<string, string>
            {
                { "Title", "Calculus I" },
                { "Description", "Comprehensive calculus course" },
                { "Price", "15000" },
                { "ThumbnailName", "calculus.jpg" },
                { "Slug", "calculus-1" },
                { "Prerequisites", "Basic algebra" },
                { "LearningOutcomes", "Master derivatives and integrals" },
                { "GradeID", gradeId.ToString() },
                { "SubjectID", subjectId.ToString() },
                { "Chapters[0].Title", "Chapter 1: Limits" },
                { "Chapters[0].Description", "Understanding limits" },
                { "Chapters[0].Order", "1" },
                { "Chapters[0].Lessons[0].Title", "Lesson 1.1: Intro to Limits" },
                { "Chapters[0].Lessons[0].Objectives", "Define limits" },
                { "Chapters[0].Lessons[0].Description", "Introductory lesson" },
                { "Chapters[0].Lessons[0].VideoUrl", "https://example.com/limits-video" },
                { "Chapters[0].Lessons[0].Order", "1" }
            };

            var fileBytes = new byte[] { 1, 2, 3 };
            var content = CreateMultipartFormContent(fields, fileBytes, "ThumbnailFile", "calculus.jpg", "image/jpeg");

            // Act
            var response = await Client.PostAsync("/api/courses", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeEmpty();

            // Verify course was created and is in review state
            await ExecuteDbContextAsync(async db =>
            {
                var course = await db.Set<Course>().FindAsync(result.Data);
                course.Should().NotBeNull();
                course!.Title.Should().Be("Calculus I");
                course.Status.Should().Be(CourseStatus.InReview);
            });
        }

        [Fact]
        public async Task CreateCourse_WithInvalidData_ReturnsBadRequest()
        {
            // Arrange
            await LoginExistingUserAsync("teacher@example.com", "Password123!");

            var fields = new Dictionary<string, string>
            {
                { "Title", "" }, // Invalid title (empty)
                { "Description", "Calculus course description" },
                { "Price", "-100" }, // Invalid price (negative)
                { "ThumbnailName", "calculus.jpg" }
            };

            var content = CreateMultipartFormContent(fields);

            // Act
            var response = await Client.PostAsync("/api/courses", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task ReviewCourse_AsAdmin_PublishesCourse()
        {
            // Arrange
            Guid teacherId = Guid.Empty;
            Guid gradeId = Guid.Empty;
            Guid subjectId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                var teacher = await db.Set<User>().FirstAsync(u => u.Email == "teacher@example.com");
                var grade = await db.Set<Grade>().FirstAsync();
                var subject = await db.Set<Subject>().FirstAsync();
                teacherId = teacher.UserID;
                gradeId = grade.GradeID;
                subjectId = subject.SubjectID;
            });

            Guid courseId = Guid.NewGuid();
            await ExecuteDbContextAsync(async db =>
            {
                var course = new Course(
                    courseId,
                    "Physics Mechanics",
                    "Mechanics course description",
                    25000,
                    "physics.jpg",
                    "physics-mechanics",
                    "Calculus I",
                    "Understand Newtonian physics",
                    teacherId,
                    gradeId,
                    subjectId,
                    DateTime.Now
                );
                db.Set<Course>().Add(course);
                await db.SaveChangesAsync();
            });

            await LoginExistingUserAsync("admin@example.com", "Password123!");

            var reviewRequest = new ReviewCourseRequestDto
            {
                CourseID = courseId,
                ViolatedPolicyIDs = new List<Guid>(), // No violations -> will publish
                AdminNote = "Looks excellent!"
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/courses/review", reviewRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewCourseResponseDto>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Course.Status.Should().Be("Published");

            // Verify status in DB
            await ExecuteDbContextAsync(async db =>
            {
                var course = await db.Set<Course>().FindAsync(courseId);
                course.Should().NotBeNull();
                course!.Status.Should().Be(CourseStatus.Published);
            });
        }

        [Fact]
        public async Task ReviewCourse_AsStudentOrTeacher_ReturnsForbidden()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            var reviewRequest = new ReviewCourseRequestDto
            {
                CourseID = Guid.NewGuid()
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/courses/review", reviewRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task CreateComplaint_AsStudent_SubmitsSuccessfully()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Guid courseId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                courseId = course.CourseID;

                var enrollment = new Enrollment(Guid.NewGuid(), student.UserID, courseId, null);
                db.Set<Enrollment>().Add(enrollment);
                await db.SaveChangesAsync();
            });

            var fields = new Dictionary<string, string>
            {
                { "CourseId", courseId.ToString() },
                { "Reason", "The course contains incorrect explanations at chapter 1." }
            };

            var fileBytes = new byte[] { 4, 5, 6 };
            var content = CreateMultipartFormContent(fields, fileBytes, "EvidenceImage", "evidence.png", "image/png");

            // Act
            var response = await Client.PostAsync("/api/courses/complaints", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<CreateComplaintResponseDto>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();

            // Verify in DB
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var complaint = await db.Set<Complaint>().FirstOrDefaultAsync(c => c.CourseID == courseId && c.StudentID == student.UserID);
                complaint.Should().NotBeNull();
                complaint!.Reason.Should().Be("The course contains incorrect explanations at chapter 1.");
                complaint.Status.Should().Be(ComplaintStatus.Pending);
            });
        }

        [Fact]
        public async Task ReviewComplaint_AsAdmin_UpdatesStatus()
        {
            // Arrange
            Guid complaintId = Guid.NewGuid();
            Guid courseId = Guid.Empty;
            Guid studentId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                courseId = course.CourseID;
                studentId = student.UserID;

                var complaint = new Complaint(complaintId, courseId, studentId, "Flawed course", "evidence.jpg");
                db.Set<Complaint>().Add(complaint);
                await db.SaveChangesAsync();
            });

            await LoginExistingUserAsync("admin@example.com", "Password123!");

            var reviewRequest = new ReviewComplaintRequestDto
            {
                ComplaintID = complaintId,
                IsApproved = true,
                AdminNote = "Validated the issue and course is suspended."
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/courses/complaints/review", reviewRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewComplaintResponseDto>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();

            // Verify in DB
            await ExecuteDbContextAsync(async db =>
            {
                var complaint = await db.Set<Complaint>().FindAsync(complaintId);
                complaint.Should().NotBeNull();
                complaint!.Status.Should().Be(ComplaintStatus.Approved);
                complaint.AdminNote.Should().Be("Validated the issue and course is suspended.");
            });
        }
    }
}
