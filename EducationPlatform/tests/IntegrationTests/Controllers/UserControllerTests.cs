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
    }
}
