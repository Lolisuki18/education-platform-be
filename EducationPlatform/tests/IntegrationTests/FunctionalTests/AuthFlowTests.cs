using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Auth;
using API.Models.Common;
using Domain.IdentityManagement.Aggregate;
using FluentAssertions;
using Infrastructure.Persistence;
using IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FunctionalTests
{
    public class AuthFlowTests : IntegrationTestBase
    {
        public AuthFlowTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task CompleteUserRegistrationAndLoginFlow_ShouldSucceed()
        {
            var email = "student_flow@example.com";
            var password = "Password123!";
            var phone = "0999888777";
            var name = "Flow User";

            // 1. Register a new user
            var registerResponse = await Client.PostAsJsonAsync("/api/auth/register", new
            {
                Email = email,
                Password = password,
                Phone = phone,
                Name = name,
                Bio = "Student studying PRM",
                Role = 1 // Student
            });
            registerResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

            // 2. Fetch the OTP from the in-memory database
            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
                var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == email);
                user.Should().NotBeNull();
                user!.IsVerified.Should().BeFalse();

                // Only a hash is stored, never the code that was e-mailed
                user.EmailOtp.Should().NotBe(TestEmailCapture.GetOtp(email));
            }

            var otp = TestEmailCapture.GetOtp(email);

            // 3. Verify the email using the OTP
            var verifyResponse = await Client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequestDto
            {
                Email = email,
                Otp = otp
            });
            verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify db state updated to verified
            using (var scope = Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
                var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == email);
                user!.IsVerified.Should().BeTrue();
            }

            // 4. Log in
            var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
            {
                Email = email,
                Password = password
            });
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
            loginResult.Should().NotBeNull();
            loginResult!.Data.Should().NotBeNull();
            loginResult.Data!.AccessToken.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task RefreshTokenAndSessionFlow_ShouldSucceed()
        {
            // 1. Create a custom web factory with ExpirySeconds set to 2
            var shortExpiryFactory = Factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "JwtSettings:ExpiryMinutes", "0.0334" } // about two seconds
                    });
                });
            });
            var shortClient = shortExpiryFactory.CreateClient();

            var email = "refresh_flow@example.com";
            var password = "Password123!";

            // Register a student on the short expiry factory's client
            var registerResponse = await shortClient.PostAsJsonAsync("/api/auth/register", new
            {
                Email = email,
                Password = password,
                Phone = "0987222333",
                Name = "Refresh User",
                Bio = "Session flow check",
                Role = 1
            });
            registerResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

            var otp = TestEmailCapture.GetOtp(email);

            var verifyResponse = await shortClient.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequestDto
            {
                Email = email,
                Otp = otp
            });
            verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Login to get token with 2-second expiry
            var loginResponse = await shortClient.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
            {
                Email = email,
                Password = password
            });
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
            var initialAccessToken = loginResult!.Data!.AccessToken;
            var refreshToken = loginResult.Data.RefreshToken;

            // Wait 1 second so that the expiration time of the refreshed token differs from the login token
            await Task.Delay(1000);

            // 2. Verify that using the refresh token gives a new token
            var refreshResponse = await shortClient.PostAsJsonAsync("/api/auth/refresh-token", refreshToken);
            refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
            var newAccessToken = refreshResult!.Data!.AccessToken;
            var newRefreshToken = refreshResult.Data.RefreshToken;

            newAccessToken.Should().NotBe(initialAccessToken);
            newRefreshToken.Should().NotBe(refreshToken);

            // 3. Verify accessing a protected resource using the new token works
            var authHeader = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newAccessToken);
            shortClient.DefaultRequestHeaders.Authorization = authHeader;
            var profileResponse = await shortClient.GetAsync("/api/user/me");
            profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Wait for access token to expire (> 2 seconds)
            await Task.Delay(3000);

            // Attempt to access profile with expired token -> should return 401 Unauthorized
            var expiredResponse = await shortClient.GetAsync("/api/user/me");
            expiredResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // 5. Perform Logout using a valid session. Since newAccessToken is expired,
            // we first refresh it to get a valid access token.
            var finalRefreshResponse = await shortClient.PostAsJsonAsync("/api/auth/refresh-token", newRefreshToken);
            finalRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var finalRefreshResult = await finalRefreshResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
            var validAccessToken = finalRefreshResult!.Data!.AccessToken;
            var finalRefreshToken = finalRefreshResult.Data.RefreshToken;

            shortClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", validAccessToken);
            var logoutResponse = await shortClient.PostAsync("/api/auth/logout", null);
            logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 6. After Logout, the refresh token is revoked. Attempting to use either the old or new refresh token should fail.
            var postLogoutRefreshResponse1 = await shortClient.PostAsJsonAsync("/api/auth/refresh-token", newRefreshToken);
            postLogoutRefreshResponse1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var postLogoutRefreshResponse2 = await shortClient.PostAsJsonAsync("/api/auth/refresh-token", finalRefreshToken);
            postLogoutRefreshResponse2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task RefreshToken_AcceptsObjectBodyAndLegacyStringBody()
        {
            var first = await LoginAsync("student@example.com", "Password123!");

            var asObject = await Client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = first.RefreshToken });
            asObject.StatusCode.Should().Be(HttpStatusCode.OK);
            var rotated = (await asObject.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>())!.Data!;

            var asString = await Client.PostAsJsonAsync("/api/auth/refresh-token", rotated.RefreshToken);
            asString.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task SecondDeviceLogin_DoesNotSignTheFirstDeviceOut()
        {
            var phone = await LoginAsync("student@example.com", "Password123!");
            var laptop = await LoginAsync("student@example.com", "Password123!");

            var phoneRefresh = await Client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = phone.RefreshToken });
            var laptopRefresh = await Client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = laptop.RefreshToken });

            phoneRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
            laptopRefresh.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Logout_WithRefreshToken_OnlySignsOutThatDevice()
        {
            var phone = await LoginAsync("student@example.com", "Password123!");
            var laptop = await LoginAsync("student@example.com", "Password123!");

            Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", phone.AccessToken);
            var logout = await Client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = phone.RefreshToken });
            logout.StatusCode.Should().Be(HttpStatusCode.OK);

            (await Client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = phone.RefreshToken }))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await Client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = laptop.RefreshToken }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ReplayedRefreshToken_RevokesEverySession()
        {
            var login = await LoginAsync("student@example.com", "Password123!");

            var rotate = await Client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = login.RefreshToken });
            var rotated = (await rotate.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>())!.Data!;

            // Presenting the old token again after the grace period looks like theft
            await Task.Delay(TimeSpan.FromSeconds(11));
            var replay = await Client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = login.RefreshToken });
            replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // ...and the legitimately rotated token is gone too
            (await Client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = rotated.RefreshToken }))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        private async Task<LoginResponseDto> LoginAsync(string email, string password)
        {
            var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto { Email = email, Password = password });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>())!.Data!;
        }
    }
}
