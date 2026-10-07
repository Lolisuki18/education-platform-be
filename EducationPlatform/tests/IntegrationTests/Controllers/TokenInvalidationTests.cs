using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Auth;
using API.Models.Common;
using FluentAssertions;
using Xunit;

namespace IntegrationTests.Controllers
{
    /// <summary>A stolen access token must stop working the moment the owner ends all sessions.</summary>
    public class TokenInvalidationTests : IntegrationTestBase
    {
        private const string OldPassword = "Password123!";
        private const string NewPassword = "Brand-new-456!";

        public TokenInvalidationTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        private async Task<LoginResponseDto> LoginAsync(string email, string password)
        {
            var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>())!.Data!;
        }

        private HttpClient ClientWith(string accessToken)
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        /// <summary>Tokens carry their issue time in whole seconds: let one pass so "issued before" is unambiguous.</summary>
        private static Task LetASecondPass() => Task.Delay(TimeSpan.FromMilliseconds(1100));

        private static async Task<HttpStatusCode> WhoAmI(HttpClient client) => (await client.GetAsync("/api/user/me")).StatusCode;

        [Fact]
        public async Task AChangedPassword_EndsTheOldAccessTokens_ButNotTheFreshOne()
        {
            await AuthenticateAsync("rotate@example.com", OldPassword, "Rotate", "0900000201");
            var stolen = (await LoginAsync("rotate@example.com", OldPassword)).AccessToken;
            (await WhoAmI(ClientWith(stolen))).Should().Be(HttpStatusCode.OK);
            await LetASecondPass();

            var change = await Client.PostAsJsonAsync("/api/auth/change-password", new { CurrentPassword = OldPassword, NewPassword });
            change.StatusCode.Should().Be(HttpStatusCode.OK);
            var fresh = (await change.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>())!.Data!;

            (await WhoAmI(ClientWith(stolen))).Should().Be(HttpStatusCode.Unauthorized);
            (await WhoAmI(ClientWith(fresh.AccessToken))).Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task AResetPassword_EndsEveryAccessToken()
        {
            await AuthenticateAsync("resetold@example.com", OldPassword, "Reset Old", "0900000202");
            var oldToken = (await LoginAsync("resetold@example.com", OldPassword)).AccessToken;
            await LetASecondPass();

            var anonymous = Factory.CreateClient();
            await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { Email = "resetold@example.com" });
            var otp = TestEmailCapture.GetResetOtp("resetold@example.com");
            (await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { Email = "resetold@example.com", Otp = otp, NewPassword }))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            (await WhoAmI(ClientWith(oldToken))).Should().Be(HttpStatusCode.Unauthorized);
            var again = await LoginAsync("resetold@example.com", NewPassword);
            (await WhoAmI(ClientWith(again.AccessToken))).Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task LoggingOutEverywhere_EndsTheOtherAccessTokens()
        {
            await AuthenticateAsync("everywhere@example.com", OldPassword, "Everywhere", "0900000203");
            var other = (await LoginAsync("everywhere@example.com", OldPassword)).AccessToken;
            await LetASecondPass();

            (await Client.PostAsync("/api/auth/logout", null)).StatusCode.Should().Be(HttpStatusCode.OK);

            (await WhoAmI(ClientWith(other))).Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task LoggingOutOneDevice_LeavesTheOtherAccessTokensAlone()
        {
            await AuthenticateAsync("onedevice@example.com", OldPassword, "One Device", "0900000204");
            var first = await LoginAsync("onedevice@example.com", OldPassword);
            var second = await LoginAsync("onedevice@example.com", OldPassword);

            var logout = await ClientWith(first.AccessToken).PostAsJsonAsync("/api/auth/logout", new { RefreshToken = first.RefreshToken });
            logout.StatusCode.Should().Be(HttpStatusCode.OK);

            (await WhoAmI(ClientWith(second.AccessToken))).Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task RefreshStillWorks_WhenTheClientAlsoSendsARevokedAccessToken()
        {
            await AuthenticateAsync("refresher@example.com", OldPassword, "Refresher", "0900000205");
            var stolen = (await LoginAsync("refresher@example.com", OldPassword)).AccessToken;
            await LetASecondPass();

            var change = await Client.PostAsJsonAsync("/api/auth/change-password", new { CurrentPassword = OldPassword, NewPassword });
            var fresh = (await change.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>())!.Data!;

            // The stale bearer header must not block the endpoint that renews a session
            var refresh = await ClientWith(stolen).PostAsJsonAsync("/api/auth/refresh-token", new { fresh.RefreshToken });

            refresh.StatusCode.Should().Be(HttpStatusCode.OK);
            var renewed = (await refresh.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>())!.Data!;
            (await WhoAmI(ClientWith(renewed.AccessToken))).Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task ADeletedAccount_LosesItsTokens()
        {
            await AuthenticateAsync("goner@example.com", OldPassword, "Goner", "0900000206");
            var token = (await LoginAsync("goner@example.com", OldPassword)).AccessToken;

            var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/user/me") { Content = JsonContent.Create(new { Password = OldPassword }) };
            (await Client.SendAsync(delete)).StatusCode.Should().Be(HttpStatusCode.OK);

            (await WhoAmI(ClientWith(token))).Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task ChangingOrResettingAPassword_EmailsTheOwner()
        {
            await AuthenticateAsync("notified@example.com", OldPassword, "Notified", "0900000207");

            await Client.PostAsJsonAsync("/api/auth/change-password", new { CurrentPassword = OldPassword, NewPassword });

            TestEmailCapture.SentEmails.Should().Contain(m => m.To == "notified@example.com" && m.Subject == "Your password was changed");
        }
    }
}
