using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Auth;
using API.Models.Common;
using FluentAssertions;
using Xunit;

namespace IntegrationTests.Controllers
{
    public class AuthControllerTests : IntegrationTestBase
    {
        public AuthControllerTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task Register_WithValidData_ReturnsAccepted()
        {
            // Arrange
            var request = new
            {
                Email = "newuser@example.com",
                Password = "Password123!",
                Phone = "0912345678",
                Name = "New User",
                Bio = "Hello world",
                Role = 1 // Student
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/auth/register", request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Message.Should().Contain("Registration successful");
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsTokens()
        {
            // Arrange
            var email = "loginuser@example.com";
            var password = "Password123!";

            // Register and verify the user first
            await AuthenticateAsync(email, password, "Login User", "0900000001", 1);

            // Reset request authorization header to test normal login
            Client.DefaultRequestHeaders.Authorization = null;

            var loginRequest = new LoginRequestDto
            {
                Email = email,
                Password = password
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.AccessToken.Should().NotBeNullOrEmpty();
            result.Data!.RefreshToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Login_WithInvalidCredentials_ReturnsError()
        {
            // Arrange
            var loginRequest = new LoginRequestDto
            {
                Email = "nonexistent@example.com",
                Password = "WrongPassword!"
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

            // Assert
            response.StatusCode.Should().Match(status =>
                status == HttpStatusCode.Unauthorized ||
                status == HttpStatusCode.BadRequest ||
                status == HttpStatusCode.NotFound ||
                status == HttpStatusCode.InternalServerError);
        }
    }
}
