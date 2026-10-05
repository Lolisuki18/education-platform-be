using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using Application.Results;
using Domain.IdentityManagement.Aggregate;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Controllers
{
    public class UserControllerTests : IntegrationTestBase
    {
        public UserControllerTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task GetMe_WhenAuthenticated_ReturnsProfile()
        {
            // Arrange
            var email = "profileuser@example.com";
            var name = "Profile User";
            var authResult = await AuthenticateAsync(email, "Password123!", name, "0900000002", 1);

            // Act
            var response = await Client.GetAsync("/api/user/me");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDTO>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Email.Should().Be(email);
            result.Data.Name.Should().Be(name);
            result.Data.UserID.Should().Be(authResult.UserId);
        }

        [Fact]
        public async Task GetMe_WhenUnauthenticated_ReturnsUnauthorized()
        {
            // Arrange
            Client.DefaultRequestHeaders.Authorization = null;

            // Act
            var response = await Client.GetAsync("/api/user/me");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task UpdateProfile_WhenAuthenticated_UpdatesData()
        {
            // Arrange
            var email = "updateuser@example.com";
            var authResult = await AuthenticateAsync(email, "Password123!", "Old Name", "0900000003", 1);

            var updateRequest = new
            {
                Name = "New Name",
                Phone = "0988888888",
                Bio = "New Bio"
            };

            // Act
            var response = await Client.PatchAsJsonAsync("/api/user/update-profile", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDTO>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data!.Name.Should().Be("New Name");
            result.Data!.Phone.Should().Be("0988888888");
            result.Data!.Bio.Should().Be("New Bio");

            // Verify in Database
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
            var user = await db.Set<User>().FindAsync(authResult.UserId);
            user.Should().NotBeNull();
            user!.Name.Should().Be("New Name");
            user.Phone.Should().Be("0988888888");
            user.Bio.Should().Be("New Bio");
        }

        [Fact]
        public async Task UpdateProfile_WhenSendingEmptyOrNullValues_PreservesExistingValues()
        {
            // Arrange
            var email = "preserveuser@example.com";
            var originalName = "Original Name";
            var originalPhone = "0900000004";
            var originalBio = "Original Bio";

            var authResult = await AuthenticateAsync(email, "Password123!", originalName, originalPhone, 1);

            // Fetch the user to ensure bio is set
            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
                var u = await db.Set<User>().FindAsync(authResult.UserId);
                u!.UpdateProfile(originalName, originalPhone, originalBio);
                await db.SaveChangesAsync();
            }

            // We send null for Name, empty string for Phone, and whitespace for Bio
            var updateRequest = new
            {
                Name = (string?)null,
                Phone = "",
                Bio = "   "
            };

            // Act
            var response = await Client.PatchAsJsonAsync("/api/user/update-profile", updateRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDTO>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();

            // The response should contain the original values
            result.Data!.Name.Should().Be(originalName);
            result.Data!.Phone.Should().Be(originalPhone);
            result.Data!.Bio.Should().Be(originalBio);

            // Verify in Database that original values are untouched
            using var scope2 = Factory.Services.CreateScope();
            var db2 = scope2.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
            var user = await db2.Set<User>().FindAsync(authResult.UserId);
            user.Should().NotBeNull();
            user!.Name.Should().Be(originalName);
            user.Phone.Should().Be(originalPhone);
            user.Bio.Should().Be(originalBio);
        }

        [Fact]
        public async Task GetUsers_AsAdmin_ReturnsPagedUsers()
        {
            // Arrange
            await LoginExistingUserAsync("admin@example.com", "Password123!");

            // Act
            var response = await Client.GetAsync("/api/user?pageIndex=1&pageSize=10");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<UserDTO>>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Items.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetUsers_AsStudent_ReturnsForbidden()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            // Act
            var response = await Client.GetAsync("/api/user?pageIndex=1&pageSize=10");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task UpdateUserStatus_DeactivateUser_PreventsDeactivatedUserAccess()
        {
            // Arrange
            // 1. Authenticate a student (valid token)
            var studentEmail = "tempstudent@example.com";
            var authResult = await AuthenticateAsync(studentEmail, "Password123!", "Temp Student", "0900000099", 1);
            var studentToken = authResult.Token;
            var studentUserId = authResult.UserId;

            // Verify they can access /me before deactivation
            var meResponseBefore = await Client.GetAsync("/api/user/me");
            meResponseBefore.StatusCode.Should().Be(HttpStatusCode.OK);

            // 2. Login as admin and deactivate the student
            await LoginExistingUserAsync("admin@example.com", "Password123!");
            var deactivateResponse = await Client.PutAsJsonAsync($"/api/user/{studentUserId}/status", new
            {
                IsActive = false
            });
            deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Re-apply the student's JWT token
            Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", studentToken);

            // 4. Try to access /me again -> should be intercepted by UserActiveMiddleware and return 403 Forbidden
            var meResponseAfter = await Client.GetAsync("/api/user/me");
            meResponseAfter.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task ChangingARole_MakesTheOldTokenAskForARefresh_ThenTheNewRoleApplies()
        {
            // A student signs in (own client, so the admin client below is independent)
            var studentClient = Factory.CreateClient();
            var login = await studentClient.PostAsJsonAsync("/api/auth/login",
                new API.Models.Auth.LoginRequestDto { Email = "student@example.com", Password = "Password123!" });
            var tokens = (await login.Content.ReadFromJsonAsync<ApiResponse<API.Models.Auth.LoginResponseDto>>())!.Data!;
            studentClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            (await studentClient.GetAsync("/api/user/me")).StatusCode.Should().Be(HttpStatusCode.OK);

            // An admin turns the student into a teacher
            var adminClient = await CreateAuthenticatedClientAsync("admin@example.com", "Password123!");
            var studentId = await ExecuteDbContextAsync(async db =>
                (await db.Set<User>().FirstAsync(u => u.Email == "student@example.com")).UserID);
            var change = await adminClient.PutAsJsonAsync($"/api/user/{studentId}/role",
                new API.Models.Users.UpdateUserRoleRequestDto { Role = Domain.IdentityManagement.Enum.Role.Teacher });
            change.StatusCode.Should().Be(HttpStatusCode.OK);

            // The old token still says "Student": the API asks the client to renew its session...
            (await studentClient.GetAsync("/api/user/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // ...and the renewed token carries the new role
            var refresh = await studentClient.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = tokens.RefreshToken });
            refresh.StatusCode.Should().Be(HttpStatusCode.OK);
            var renewed = (await refresh.Content.ReadFromJsonAsync<ApiResponse<API.Models.Auth.LoginResponseDto>>())!.Data!;
            studentClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", renewed.AccessToken);
            (await studentClient.GetAsync("/api/user/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
