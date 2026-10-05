using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Domain.AuditManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.NotificationManagement.Aggregate;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Controllers
{
    /// <summary>Right of access (download my data) and right to erasure (delete my account).</summary>
    public class AccountPrivacyTests : IntegrationTestBase
    {
        private const string Password = "Password123!";

        public AccountPrivacyTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        private async Task<Guid> UserIdAsync(string email)
            => await ExecuteDbContextAsync(db => db.Set<User>().Where(u => u.Email == email).Select(u => u.UserID).FirstAsync());

        private async Task AddEnrollmentAndNotificationAsync(string email)
        {
            await ExecuteDbContextAsync(async db =>
            {
                var user = await db.Set<User>().FirstAsync(u => u.Email == email);
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");

                db.Set<Enrollment>().Add(new Enrollment(Guid.NewGuid(), user.UserID, course.CourseID, null));
                db.Set<Notification>().Add(new Notification(user.UserID, "Welcome", "A private message"));
                await db.SaveChangesAsync();
            });
        }

        private static HttpRequestMessage DeleteWithPassword(string url, string password)
            => new(HttpMethod.Delete, url) { Content = JsonContent.Create(new { Password = password }) };

        // ------------------------------------------------------------------ export

        [Fact]
        public async Task Export_ReturnsTheCallersOwnRecords_AndNoSecrets()
        {
            await LoginExistingUserAsync("student@example.com", Password);
            await AddEnrollmentAndNotificationAsync("student@example.com");
            await AddEnrollmentAndNotificationAsync("teacher@example.com"); // somebody else's data must not leak in

            var response = await Client.GetAsync("/api/user/me/export");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentDisposition?.DispositionType.Should().Be("attachment");

            var json = await response.Content.ReadAsStringAsync();
            json.Should().Contain("student@example.com");
            json.Should().Contain("Math algebra");
            json.Should().Contain("A private message");
            json.Should().NotContain("teacher@example.com");

            // Hashes of passwords, one-time codes and refresh tokens never leave the server
            json.Should().NotContain("$2a$").And.NotContain("$2b$");
            json.ToLowerInvariant().Should().NotContain("passwordhash").And.NotContain("emailotp").And.NotContain("refresh");
        }

        [Fact]
        public async Task Export_RequiresSignIn()
        {
            (await Client.GetAsync("/api/user/me/export")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // ------------------------------------------------------------------ erasure

        [Fact]
        public async Task DeleteMyAccount_WithAWrongPassword_ChangesNothing()
        {
            await AuthenticateAsync("keeper@example.com", Password, "Keeper", "0900000011");

            var response = await Client.SendAsync(DeleteWithPassword("/api/user/me", "Not-my-password1"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ExecuteDbContextAsync(db => db.Set<User>().AnyAsync(u => u.Email == "keeper@example.com" && u.DeletedAt == null)))
                .Should().BeTrue();
        }

        [Fact]
        public async Task DeleteMyAccount_ErasesPersonalData_ButKeepsTheRecordsThatPointToIt()
        {
            await AuthenticateAsync("leaving@example.com", Password, "Leaving Person", "0900000022");
            var userId = await UserIdAsync("leaving@example.com");
            await AddEnrollmentAndNotificationAsync("leaving@example.com");

            var response = await Client.SendAsync(DeleteWithPassword("/api/user/me", Password));

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var user = await ExecuteDbContextAsync(db => db.Set<User>().AsNoTracking().FirstAsync(u => u.UserID == userId));
            user.IsDeleted.Should().BeTrue();
            user.IsActive.Should().BeFalse();
            user.Email.Should().EndWith("@deleted.invalid");
            user.Name.Should().Be(User.DeletedName);
            user.Bio.Should().BeNull();

            // The enrollment stays (history of the course), the private notification does not
            (await ExecuteDbContextAsync(db => db.Set<Enrollment>().CountAsync(e => e.StudentID == userId))).Should().Be(1);
            (await ExecuteDbContextAsync(db => db.Set<Notification>().CountAsync(n => n.UserID == userId))).Should().Be(0);

            // Neither the audit trail nor anything else still holds the old address
            var audit = await ExecuteDbContextAsync(db => db.Set<AuditLog>().ToListAsync());
            audit.Select(a => (a.OldValue ?? string.Empty) + (a.NewValue ?? string.Empty))
                .Should().NotContain(v => v.Contains("leaving@example.com") || v.Contains("Leaving Person"));
        }

        [Fact]
        public async Task AfterDeletion_TheOldSessionAndCredentialsStopWorking_AndTheAddressCanBeUsedAgain()
        {
            await AuthenticateAsync("again@example.com", Password, "Again", "0900000033");
            (await Client.SendAsync(DeleteWithPassword("/api/user/me", Password))).StatusCode.Should().Be(HttpStatusCode.OK);

            // The access token is still valid cryptographically, but the account is gone
            (await Client.GetAsync("/api/user/me")).StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);

            var anonymous = Factory.CreateClient();
            var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { Email = "again@example.com", Password });
            login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // The e-mail address and phone number were released
            var register = await anonymous.PostAsJsonAsync("/api/auth/register", new
            {
                Email = "again@example.com",
                Password,
                Phone = "0900000033",
                Name = "Again, new account",
                Role = 1
            });
            register.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await ExecuteDbContextAsync(db => db.Set<User>().CountAsync(u => u.Email == "again@example.com" && u.DeletedAt == null)))
                .Should().Be(1);
        }

        [Fact]
        public async Task DeleteMyAccount_LastAdministrator_IsRefused()
        {
            await LoginExistingUserAsync("admin@example.com", Password);

            var response = await Client.SendAsync(DeleteWithPassword("/api/user/me", Password));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task DeleteMyAccount_RequiresSignIn()
        {
            var response = await Client.SendAsync(DeleteWithPassword("/api/user/me", Password));

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // ------------------------------------------------------------------ erasure by an administrator

        [Fact]
        public async Task Administrator_CanEraseAnotherAccount()
        {
            var studentId = await UserIdAsync("student@example.com");
            await LoginExistingUserAsync("admin@example.com", Password);

            var response = await Client.DeleteAsync($"/api/user/{studentId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ExecuteDbContextAsync(db => db.Set<User>().AnyAsync(u => u.UserID == studentId && u.DeletedAt != null)))
                .Should().BeTrue();
        }

        [Fact]
        public async Task Administrator_CannotUseTheAdminEndpointOnThemselves()
        {
            var adminId = await UserIdAsync("admin@example.com");
            await LoginExistingUserAsync("admin@example.com", Password);

            (await Client.DeleteAsync($"/api/user/{adminId}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task NormalUsers_CannotEraseOtherAccounts()
        {
            var teacherId = await UserIdAsync("teacher@example.com");
            await LoginExistingUserAsync("student@example.com", Password);

            (await Client.DeleteAsync($"/api/user/{teacherId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
