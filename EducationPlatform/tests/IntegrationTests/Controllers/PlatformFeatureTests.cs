using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using Application.Features.Notifications;
using Application.Interface;
using Application.Results;
using Domain.AuditManagement.Aggregate;
using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.NotificationManagement.Aggregate;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Controllers
{
    /// <summary>End-to-end checks of the protected media, notifications, caching and data-integrity features.</summary>
    public class PlatformFeatureTests : IntegrationTestBase
    {
        public PlatformFeatureTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        private static string StorageRoot => Path.Combine(Directory.GetCurrentDirectory(), "test_storage");

        // ------------------------------------------------------------------ protected media

        [Fact]
        public async Task UploadedVideo_IsForbiddenWithoutASignedLink_AndServedWithOne()
        {
            Directory.CreateDirectory(Path.Combine(StorageRoot, "videos"));
            var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            await File.WriteAllBytesAsync(Path.Combine(StorageRoot, "videos", "protected-lesson.mp4"), bytes);

            var anonymous = await Client.GetAsync("/media/videos/protected-lesson.mp4");
            anonymous.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // A signature for a different file is no use
            var signer = Factory.Services.GetRequiredService<IMediaUrlSigner>();
            var wrongFile = signer.Protect("videos/another.mp4");
            (await Client.GetAsync("/media/videos/protected-lesson.mp4" + wrongFile[wrongFile.IndexOf('?')..]))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var signed = signer.Protect("videos/protected-lesson.mp4");
            var allowed = await Client.GetAsync("/media/" + signed);
            allowed.StatusCode.Should().Be(HttpStatusCode.OK);
            (await allowed.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
            allowed.Headers.CacheControl!.NoStore.Should().BeTrue();
        }

        [Fact]
        public async Task ASignedVideoLink_SupportsRangeRequests()
        {
            Directory.CreateDirectory(Path.Combine(StorageRoot, "videos"));
            await File.WriteAllBytesAsync(Path.Combine(StorageRoot, "videos", "range.mp4"), Enumerable.Range(0, 100).Select(i => (byte)i).ToArray());
            var signed = Factory.Services.GetRequiredService<IMediaUrlSigner>().Protect("videos/range.mp4");

            var request = new HttpRequestMessage(HttpMethod.Get, "/media/" + signed);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(10, 19);
            var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(Enumerable.Range(10, 10).Select(i => (byte)i));
        }

        [Fact]
        public async Task OtherMedia_StaysPublic()
        {
            Directory.CreateDirectory(Path.Combine(StorageRoot, "2026", "06"));
            await File.WriteAllBytesAsync(Path.Combine(StorageRoot, "2026", "06", "thumb.png"), new byte[] { 0x89, 0x50, 0x4E, 0x47 });

            (await Client.GetAsync("/media/2026/06/thumb.png")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task EnrollmentDetail_ReturnsSignedLinksForStoredVideos_AndLeavesExternalOnesAlone()
        {
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Guid enrollmentId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");

                // One lesson hosted on the platform, one external
                var chapter = await db.Set<Domain.CourseManagement.Entity.Chapter>().FirstAsync(c => c.CourseID == course.CourseID);
                var lesson = await db.Set<Domain.CourseManagement.Entity.Lesson>().FirstAsync(l => l.ChapterID == chapter.ChapterID);
                db.Entry(lesson).Property(l => l.VideoUrl).CurrentValue = "videos/stored-lesson.mp4";

                var enrollment = new Enrollment(Guid.NewGuid(), student.UserID, course.CourseID, null);
                db.Set<Enrollment>().Add(enrollment);
                await db.SaveChangesAsync();
                enrollmentId = enrollment.EnrollmentID;
            });

            var response = await Client.GetAsync($"/api/enrollments/{enrollmentId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadAsStringAsync();
            json.Should().Contain("videos/stored-lesson.mp4?exp=");
            json.Should().Contain("&sig=");
        }

        // ------------------------------------------------------------------ notifications

        private async Task<Guid> AddNotificationAsync(string email, string title, bool read = false)
        {
            return await ExecuteDbContextAsync(async db =>
            {
                var user = await db.Set<User>().FirstAsync(u => u.Email == email);
                var notification = new Notification(user.UserID, title, "Body of " + title);
                if (read) notification.MarkAsRead();
                db.Set<Notification>().Add(notification);
                await db.SaveChangesAsync();
                return notification.NotificationID;
            });
        }

        [Fact]
        public async Task Notifications_ListCountAndMarkRead()
        {
            await LoginExistingUserAsync("student@example.com", "Password123!");
            var first = await AddNotificationAsync("student@example.com", "First");
            await AddNotificationAsync("student@example.com", "Second");
            await AddNotificationAsync("student@example.com", "Already read", read: true);
            await AddNotificationAsync("teacher@example.com", "Not mine");

            var count = await Client.GetFromJsonAsync<ApiResponse<int>>("/api/notifications/unread-count");
            count!.Data.Should().Be(2);

            var all = await Client.GetFromJsonAsync<ApiResponse<PagedResult<NotificationDTO>>>("/api/notifications");
            all!.Data!.TotalItems.Should().Be(3);
            all.Data.Items.Select(n => n.Title).Should().NotContain("Not mine");

            var unread = await Client.GetFromJsonAsync<ApiResponse<PagedResult<NotificationDTO>>>("/api/notifications?unreadOnly=true");
            unread!.Data!.Items.Should().HaveCount(2).And.OnlyContain(n => !n.IsRead);

            (await Client.PostAsync($"/api/notifications/{first}/read", null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await Client.GetFromJsonAsync<ApiResponse<int>>("/api/notifications/unread-count"))!.Data.Should().Be(1);

            (await Client.PostAsync("/api/notifications/read-all", null)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await Client.GetFromJsonAsync<ApiResponse<int>>("/api/notifications/unread-count"))!.Data.Should().Be(0);
        }

        [Fact]
        public async Task Notifications_OfSomeoneElse_CannotBeTouched()
        {
            await LoginExistingUserAsync("student@example.com", "Password123!");
            var theirs = await AddNotificationAsync("teacher@example.com", "Private");

            (await Client.PostAsync($"/api/notifications/{theirs}/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Notifications_RequireSignIn()
        {
            Client.DefaultRequestHeaders.Authorization = null;

            (await Client.GetAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task SubmittingACourse_TellsTheAdmins()
        {
            await LoginExistingUserAsync("teacher@example.com", "Password123!");

            Guid gradeId = Guid.Empty, subjectId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                gradeId = (await db.Set<Grade>().FirstAsync()).GradeID;
                subjectId = (await db.Set<Subject>().FirstAsync()).SubjectID;
            });

            var response = await Client.PostAsync("/api/courses", CourseForm("Notify the admins", "notify-the-admins", gradeId, subjectId));
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Delivery happens right after the commit, in the background of the request
            var delivered = await Eventually(async () => await ExecuteDbContextAsync(db =>
                db.Set<Notification>().AnyAsync(n => n.Title == "New course waiting for review")));
            delivered.Should().BeTrue();

            var admin = await ExecuteDbContextAsync(db => db.Set<User>().FirstAsync(u => u.Email == "admin@example.com"));
            (await ExecuteDbContextAsync(db => db.Set<Notification>().CountAsync(n => n.UserID == admin.UserID))).Should().BeGreaterThan(0);

            // ...and the e-mail copy went through the (captured) mail service
            TestEmailCapture.SentEmails.Should().Contain(e => e.To == "admin@example.com" && e.Subject == "New course waiting for review");
        }

        private static async Task<bool> Eventually(Func<Task<bool>> check, int attempts = 40)
        {
            for (var i = 0; i < attempts; i++)
            {
                if (await check()) return true;
                await Task.Delay(100);
            }
            return false;
        }

        // ------------------------------------------------------------------ caching & compression

        [Fact]
        public async Task PublicListings_AreCachedForAnonymousVisitorsOnly()
        {
            var cachingFactory = Factory.WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?> { { "OutputCache:Enabled", "true" } })));
            var anonymous = cachingFactory.CreateClient();

            var before = (await anonymous.GetFromJsonAsync<ApiResponse<List<GradeDTO>>>("/api/academic/grades"))!.Data!.Count;

            await ExecuteDbContextAsync(async db =>
            {
                db.Set<Grade>().Add(new Grade(Guid.NewGuid(), "Grade 99"));
                await db.SaveChangesAsync();
            });

            // The anonymous answer is reused...
            (await anonymous.GetFromJsonAsync<ApiResponse<List<GradeDTO>>>("/api/academic/grades"))!.Data!.Count.Should().Be(before);

            // ...but somebody who is signed in always gets fresh data
            var signedIn = cachingFactory.CreateClient();
            var login = await signedIn.PostAsJsonAsync("/api/auth/login",
                new API.Models.Auth.LoginRequestDto { Email = "admin@example.com", Password = "Password123!" });
            var token = (await login.Content.ReadFromJsonAsync<ApiResponse<API.Models.Auth.LoginResponseDto>>())!.Data!.AccessToken;
            signedIn.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            (await signedIn.GetFromJsonAsync<ApiResponse<List<GradeDTO>>>("/api/academic/grades"))!.Data!.Count.Should().Be(before + 1);
        }

        [Fact]
        public async Task Responses_AreCompressedWhenTheClientAsksForIt()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/academic/grades");
            request.Headers.AcceptEncoding.ParseAdd("gzip");

            var response = await Client.SendAsync(request);

            response.Content.Headers.ContentEncoding.Should().Contain("gzip");
        }

        // ------------------------------------------------------------------ integrity

        private MultipartFormDataContent CourseForm(string title, string slug, Guid gradeId, Guid subjectId, string videoUrl = "https://example.com/video")
        {
            var fields = new Dictionary<string, string>
            {
                { "Title", title },
                { "Description", "A course" },
                { "Price", "15000" },
                { "ThumbnailName", "thumb.jpg" },
                { "Slug", slug },
                { "Prerequisites", "None" },
                { "LearningOutcomes", "Everything" },
                { "GradeID", gradeId.ToString() },
                { "SubjectID", subjectId.ToString() },
                { "Chapters[0].Title", "Chapter 1" },
                { "Chapters[0].Description", "First" },
                { "Chapters[0].Order", "1" },
                { "Chapters[0].Lessons[0].Title", "Lesson 1" },
                { "Chapters[0].Lessons[0].Objectives", "Learn" },
                { "Chapters[0].Lessons[0].Description", "Intro" },
                { "Chapters[0].Lessons[0].VideoUrl", videoUrl },
                { "Chapters[0].Lessons[0].Order", "1" }
            };

            return CreateMultipartFormContent(fields, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0 }, "ThumbnailFile", "thumb.jpg", "image/jpeg");
        }

        [Fact]
        public async Task TwoCoursesWithTheSameSlug_GetDifferentOnes()
        {
            await LoginExistingUserAsync("teacher@example.com", "Password123!");

            Guid gradeId = Guid.Empty, subjectId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                gradeId = (await db.Set<Grade>().FirstAsync()).GradeID;
                subjectId = (await db.Set<Subject>().FirstAsync()).SubjectID;
            });

            var first = await Client.PostAsync("/api/courses", CourseForm("Same Title", "same-slug", gradeId, subjectId));
            var second = await Client.PostAsync("/api/courses", CourseForm("Same Title", "same-slug", gradeId, subjectId));

            first.StatusCode.Should().Be(HttpStatusCode.OK);
            second.StatusCode.Should().Be(HttpStatusCode.OK);

            var slugs = await ExecuteDbContextAsync(db => db.Set<Course>().Where(c => c.Title == "Same Title").Select(c => c.Slug).ToListAsync());
            slugs.Should().HaveCount(2);
            slugs.Distinct().Should().HaveCount(2);
            slugs.Should().Contain("same-slug");
        }

        [Theory]
        [InlineData("javascript:alert(1)")]
        [InlineData("data:text/html,<script>alert(1)</script>")]
        [InlineData("http://plain-http.example.com/video")]
        public async Task LessonVideoLinks_WithDangerousSchemes_AreRejected(string videoUrl)
        {
            await LoginExistingUserAsync("teacher@example.com", "Password123!");

            Guid gradeId = Guid.Empty, subjectId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                gradeId = (await db.Set<Grade>().FirstAsync()).GradeID;
                subjectId = (await db.Set<Subject>().FirstAsync()).SubjectID;
            });

            var response = await Client.PostAsync("/api/courses", CourseForm("Bad Link", "bad-link", gradeId, subjectId, videoUrl));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ExecuteDbContextAsync(db => db.Set<Course>().AnyAsync(c => c.Title == "Bad Link"))).Should().BeFalse();
        }

        [Fact]
        public async Task TheDatabase_RefusesASecondEnrollmentOfTheSameStudentInTheSameCourse()
        {
            Func<Task> act = async () => await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");

                db.Set<Enrollment>().Add(new Enrollment(Guid.NewGuid(), student.UserID, course.CourseID, null));
                db.Set<Enrollment>().Add(new Enrollment(Guid.NewGuid(), student.UserID, course.CourseID, null));
                await db.SaveChangesAsync();
            });

            var thrown = await act.Should().ThrowAsync<DbUpdateException>();
            thrown.Which.InnerException.Should().BeOfType<Npgsql.PostgresException>()
                .Which.SqlState.Should().Be(Npgsql.PostgresErrorCodes.UniqueViolation);
        }

        [Fact]
        public async Task TheAuditTrail_NeverContainsPasswordOrSessionHashes()
        {
            // Registering writes a user (with its password) and logging in writes a refresh session
            await AuthenticateAsync("audited@example.com", "Password123!", "Audited", "0900000099", 1);

            var logs = await ExecuteDbContextAsync(db => db.Set<AuditLog>().ToListAsync());

            logs.Should().Contain(l => l.EntityName == "User");
            logs.Select(l => l.EntityName).Should().NotContain(new[] { "Password", "RefreshSession" });
            logs.Select(l => l.NewValue ?? string.Empty).Should().NotContain(v => v.Contains("$2a$") || v.Contains("$2b$"));
        }
    }
}
