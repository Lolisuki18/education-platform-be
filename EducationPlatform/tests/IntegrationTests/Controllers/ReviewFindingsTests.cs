using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using Application.Results;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Domain.EnrollmentManagement.Aggregate;
using Domain.EnrollmentManagement.Entity;
using Domain.IdentityManagement.Aggregate;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Controllers
{
    /// <summary>Regression tests for the problems found in the full backend review before the frontend work.</summary>
    public class ReviewFindingsTests : IntegrationTestBase
    {
        private static readonly Guid SeededChapterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid SeededLessonId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        public ReviewFindingsTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        // ---------- Teacher contact details must not be public ----------

        [Fact]
        public async Task PublicCourseListing_DoesNotExposeTheTeachersContactDetails()
        {
            var response = await Client.GetAsync("/api/courses");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Math algebra");
            body.Should().NotContain("teacher@example.com");
            body.Should().NotContain("0911111111");
        }

        [Fact]
        public async Task PublicCourseDetail_DoesNotExposeTheTeachersContactDetails()
        {
            var courseId = await ExecuteDbContextAsync(async db =>
                (await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra")).CourseID);

            var response = await Client.GetAsync($"/api/courses/{courseId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("teacher@example.com");
            body.Should().NotContain("0911111111");

            var course = (await response.Content.ReadFromJsonAsync<ApiResponse<CourseDetailDTO>>())!.Data!;
            course.Teacher.Name.Should().Be("Teacher User");
        }

        [Fact]
        public async Task EnrollmentList_DoesNotExposeTheTeachersContactDetails()
        {
            var client = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");
            await EnrollStudentAsync();

            var response = await client.GetAsync("/api/enrollments");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().NotContain("teacher@example.com");
            body.Should().NotContain("0911111111");
        }

        // ---------- Quiz answers must not reach the student ----------

        [Fact]
        public async Task EnrollmentDetail_DoesNotRevealTheCorrectAnswersOfAnyQuiz()
        {
            var client = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");
            var enrollmentId = await EnrollStudentAsync();

            await ExecuteDbContextAsync(async db =>
            {
                var lesson = await db.Set<Lesson>().FirstAsync(l => l.LessonID == SeededLessonId);
                var quiz = lesson.AddQuiz("What is 1 + 1?", "Because two ones make two");
                quiz.AddAnswer(QuizType.SingleChoice, new[] { "2" }, new[] { "1", "2", "3" });
                db.Set<Quiz>().Add(quiz);
                await db.SaveChangesAsync();
            });

            var response = await client.GetAsync($"/api/enrollments/{enrollmentId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("What is 1 + 1?");
            body.Should().Contain("\"options\":[\"1\",\"2\",\"3\"]"); // the question stays answerable
            body.Should().NotContain("Because two ones make two"); // the explanation comes with the result

            using var json = System.Text.Json.JsonDocument.Parse(body);
            var answers = new System.Collections.Generic.List<System.Text.Json.JsonElement>();
            CollectProperty(json.RootElement, "correctAnswers", answers);
            answers.Should().NotBeEmpty();
            answers.Should().OnlyContain(a => a.GetArrayLength() == 0);
        }

        private static void CollectProperty(System.Text.Json.JsonElement element, string name, System.Collections.Generic.List<System.Text.Json.JsonElement> found)
        {
            switch (element.ValueKind)
            {
                case System.Text.Json.JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                            found.Add(property.Value);
                        CollectProperty(property.Value, name, found);
                    }
                    break;
                case System.Text.Json.JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        CollectProperty(item, name, found);
                    break;
            }
        }

        // ---------- Progress is measured against the whole course ----------

        [Fact]
        public async Task CompletingOneLesson_OfATwoLessonCourse_IsHalfwayNotDone()
        {
            var client = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");
            var enrollmentId = await EnrollStudentAsync();
            await AddSecondLessonAsync();

            var response = await client.PostAsJsonAsync("/api/enrollments/progress/lesson", new
            {
                EnrollmentID = enrollmentId,
                ChapterID = SeededChapterId,
                LessonID = SeededLessonId,
                IsCompleted = true
            });
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            await ExecuteDbContextAsync(async db =>
            {
                var progress = await db.Set<CourseProgress>().SingleAsync(p => p.EnrollmentID == enrollmentId);
                progress.CompletionRate.Should().Be(50);
                progress.IsCompleted.Should().BeFalse();

                var enrollment = await db.Set<Enrollment>().FindAsync(enrollmentId);
                enrollment!.CompletedAt.Should().BeNull();
            });
        }

        [Fact]
        public async Task OpeningALesson_WithoutCompletingIt_DoesNotCompleteIt()
        {
            var client = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");
            var enrollmentId = await EnrollStudentAsync();

            var response = await client.PostAsJsonAsync("/api/enrollments/progress/lesson", new
            {
                EnrollmentID = enrollmentId,
                ChapterID = SeededChapterId,
                LessonID = SeededLessonId,
                IsCompleted = false
            });
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            await ExecuteDbContextAsync(async db =>
            {
                var progress = await db.Set<CourseProgress>().SingleAsync(p => p.EnrollmentID == enrollmentId);
                progress.IsCompleted.Should().BeFalse();
                progress.CompletionRate.Should().Be(0);
            });
        }

        [Fact]
        public async Task AnsweringOneQuizCorrectly_DoesNotCompleteALessonWithTwoQuizzes()
        {
            var client = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");
            var enrollmentId = await EnrollStudentAsync();

            Guid firstQuiz = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                var lesson = await db.Set<Lesson>().FirstAsync(l => l.LessonID == SeededLessonId);
                var q1 = lesson.AddQuiz("Q1", null);
                q1.AddAnswer(QuizType.SingleChoice, new[] { "a" }, new[] { "a", "b" });
                var q2 = lesson.AddQuiz("Q2", null);
                q2.AddAnswer(QuizType.SingleChoice, new[] { "a" }, new[] { "a", "b" });
                db.Set<Quiz>().AddRange(q1, q2);
                await db.SaveChangesAsync();
                firstQuiz = q1.QuizID;
            });

            var response = await client.PostAsJsonAsync("/api/enrollments/progress/quiz", new
            {
                EnrollmentID = enrollmentId,
                ChapterID = SeededChapterId,
                LessonID = SeededLessonId,
                QuizID = firstQuiz,
                SelectedAnswers = new[] { "a" }
            });
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            await ExecuteDbContextAsync(async db =>
            {
                var progress = await db.Set<CourseProgress>().SingleAsync(p => p.EnrollmentID == enrollmentId);
                progress.IsCompleted.Should().BeFalse();
                progress.CompletionRate.Should().Be(0);
            });
        }

        [Fact]
        public async Task Progress_CannotBeRecordedForALessonOfAnotherCourse()
        {
            var client = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");
            var enrollmentId = await EnrollStudentAsync();

            var otherCourseLesson = await ExecuteDbContextAsync(async db =>
            {
                var geometry = await db.Set<Course>().FirstAsync(c => c.Title == "Math geometry");
                var chapter = geometry.AddChapter("Other chapter", "Other", 1);
                var lesson = chapter.AddLesson("Other lesson", "o", "o", "https://example.com/other");
                db.Set<Chapter>().Add(chapter);
                db.Set<Lesson>().Add(lesson);
                await db.SaveChangesAsync();
                return (chapter.ChapterID, lesson.LessonID);
            });

            var response = await client.PostAsJsonAsync("/api/enrollments/progress/lesson", new
            {
                EnrollmentID = enrollmentId,
                ChapterID = otherCourseLesson.Item1,
                LessonID = otherCourseLesson.Item2,
                IsCompleted = true
            });

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Progress_WithUnknownIds_IsRejectedNotAServerError()
        {
            var client = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");
            var enrollmentId = await EnrollStudentAsync();

            var lesson = await client.PostAsJsonAsync("/api/enrollments/progress/lesson", new
            {
                EnrollmentID = enrollmentId,
                ChapterID = Guid.NewGuid(),
                LessonID = Guid.NewGuid(),
                IsCompleted = true
            });
            lesson.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var quiz = await client.PostAsJsonAsync("/api/enrollments/progress/quiz", new
            {
                EnrollmentID = enrollmentId,
                ChapterID = SeededChapterId,
                LessonID = SeededLessonId,
                QuizID = Guid.NewGuid(),
                SelectedAnswers = new[] { "x" }
            });
            quiz.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        // ---------- Only published courses can be bought ----------

        [Fact]
        public async Task ACourseThatIsStillInReview_CannotBeOrdered()
        {
            var client = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");

            var inReviewId = await ExecuteDbContextAsync(async db =>
                (await db.Set<Course>().FirstAsync(c => c.Title == "Math geometry")).CourseID);

            var response = await client.PostAsJsonAsync("/api/orders", new { CourseId = inReviewId });

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await ExecuteDbContextAsync(db => db.Orders.AnyAsync(o => o.CourseID == inReviewId))).Should().BeFalse();
        }

        // ---------- Deactivated accounts cannot sign in ----------

        [Fact]
        public async Task ADeactivatedAccount_GetsNoTokensAtLogin()
        {
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                student.Deactivate();
                await db.SaveChangesAsync();
            });

            var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = "student@example.com", Password = "Password123!" });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // ---------- E-mail addresses are not case sensitive ----------

        [Fact]
        public async Task AnAddress_TypedWithCapitalsAndPadding_ReachesTheSameAccount()
        {
            var register = await Client.PostAsJsonAsync("/api/auth/register", new
            {
                Email = "  Mixed.Case@Example.com ",
                Password = "Password123!",
                Phone = "0987000111",
                Name = "Mixed Case",
                Role = 1
            });
            register.StatusCode.Should().Be(HttpStatusCode.Accepted);

            // The mail went to the stored (normalized) address
            var otp = TestEmailCapture.GetOtp("mixed.case@example.com");
            var verify = await Client.PostAsJsonAsync("/api/auth/verify-email", new { Email = "MIXED.CASE@example.com", Otp = otp });
            verify.StatusCode.Should().Be(HttpStatusCode.OK);

            var login = await Client.PostAsJsonAsync("/api/auth/login", new { Email = "Mixed.Case@EXAMPLE.com", Password = "Password123!" });
            login.StatusCode.Should().Be(HttpStatusCode.OK);

            var again = await Client.PostAsJsonAsync("/api/auth/register", new
            {
                Email = "mixed.case@example.com",
                Password = "Password123!",
                Phone = "0987000222",
                Name = "Second",
                Role = 1
            });
            again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        // ---------- Profile updates follow the registration rules ----------

        [Fact]
        public async Task UpdateProfile_WithAPhoneAnotherAccountOwns_IsAConflict()
        {
            var client = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");

            var response = await client.PatchAsJsonAsync("/api/user/update-profile", new { Phone = "0911111111" }); // the teacher's

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Theory]
        [InlineData("12345")]
        [InlineData("not-a-phone")]
        public async Task UpdateProfile_WithAnInvalidPhone_IsABadRequest(string phone)
        {
            var client = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");

            var response = await client.PatchAsJsonAsync("/api/user/update-profile", new { Phone = phone });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task UpdateProfile_KeepingYourOwnPhone_IsFine()
        {
            var client = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");

            var response = await client.PatchAsJsonAsync("/api/user/update-profile", new { Name = "Renamed", Phone = "0922222222" });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // ---------- Report parameters are validated ----------

        [Theory]
        [InlineData("/api/statistics/analytics/growth?type=99&groupBy=Day")]
        [InlineData("/api/statistics/analytics/growth?type=0&groupBy=99")]
        [InlineData("/api/statistics/analytics/top-performance?top=0")]
        [InlineData("/api/statistics/analytics/top-performance?top=100000")]
        [InlineData("/api/statistics/summary?from=2026-02-01&to=2026-01-01")]
        public async Task ANonsenseReportRequest_IsABadRequestNotAServerError(string url)
        {
            var admin = await CreateAuthenticatedClientAsync("admin@example.com", "Password123!");

            var response = await admin.GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // ---------- Chunked video upload ----------

        private static readonly byte[] Mp4Header = { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D };

        private async Task<HttpResponseMessage> SendChunkAsync(HttpClient client, string uploadId, int index, byte[] bytes, string fileName = "blob")
        {
            var content = new MultipartFormDataContent
            {
                { new StringContent(uploadId), "UploadId" },
                { new StringContent(index.ToString()), "Index" }
            };
            content.Add(new ByteArrayContent(bytes), "Chunk", fileName);
            return await client.PostAsync("/api/courses/upload-chunk", content);
        }

        private static async Task<HttpResponseMessage> CompleteUploadAsync(HttpClient client, string uploadId) =>
            await client.PostAsync("/api/courses/complete-upload", new MultipartFormDataContent
            {
                { new StringContent(uploadId), "UploadId" },
                { new StringContent("mp4"), "Extension" }
            });

        [Fact]
        public async Task AVideo_SentInSeveralChunks_CanBeUploaded()
        {
            var teacher = await CreateAuthenticatedClientAsync("teacher@example.com", "Password123!");
            var uploadId = Guid.NewGuid().ToString();

            // Only the first chunk of a video starts with the file signature; the others are raw slices of it
            (await SendChunkAsync(teacher, uploadId, 0, Mp4Header.Concat(new byte[] { 1, 2, 3 }).ToArray())).StatusCode.Should().Be(HttpStatusCode.OK);
            (await SendChunkAsync(teacher, uploadId, 1, new byte[] { 9, 8, 7, 6, 5, 4 })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await SendChunkAsync(teacher, uploadId, 2, new byte[] { 0x25, 0x50, 0x44, 0x46, 1 })).StatusCode.Should().Be(HttpStatusCode.OK);

            var complete = await CompleteUploadAsync(teacher, uploadId);

            complete.StatusCode.Should().Be(HttpStatusCode.OK, await complete.Content.ReadAsStringAsync());
            (await complete.Content.ReadAsStringAsync()).Should().Contain($"videos/{uploadId}.mp4");
        }

        [Fact]
        public async Task ACourseThumbnail_MustBeAnImage()
        {
            var teacher = await CreateAuthenticatedClientAsync("teacher@example.com", "Password123!");
            var (gradeId, subjectId) = await ExecuteDbContextAsync(async db =>
                ((await db.Set<Domain.AcademicManagement.Aggregate.Grade>().FirstAsync()).GradeID,
                 (await db.Set<Domain.AcademicManagement.Aggregate.Subject>().FirstAsync()).SubjectID));

            var content = CreateMultipartFormContent(new System.Collections.Generic.Dictionary<string, string>
            {
                { "Title", "Zip thumbnail" }, { "Description", "d" }, { "Price", "1000" }, { "Slug", "zip-thumb" },
                { "Prerequisites", "p" }, { "LearningOutcomes", "l" },
                { "GradeID", gradeId.ToString() }, { "SubjectID", subjectId.ToString() }
            }, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0, 0, 0, 0 }, "ThumbnailFile", "payload.zip", "application/zip");

            var response = await teacher.PostAsync("/api/courses", content);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // ---------- helpers ----------

        private async Task<Guid> EnrollStudentAsync()
        {
            return await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");
                var enrollment = new Enrollment(Guid.NewGuid(), student.UserID, course.CourseID, null);
                db.Set<Enrollment>().Add(enrollment);
                await db.SaveChangesAsync();
                return enrollment.EnrollmentID;
            });
        }

        private async Task AddSecondLessonAsync()
        {
            await ExecuteDbContextAsync(async db =>
            {
                var chapter = await db.Set<Chapter>().FirstAsync(c => c.ChapterID == SeededChapterId);
                var lesson = chapter.AddLesson("Lesson 2", "o", "d", "https://example.com/video2");
                db.Set<Lesson>().Add(lesson);
                await db.SaveChangesAsync();
            });
        }
    }
}
