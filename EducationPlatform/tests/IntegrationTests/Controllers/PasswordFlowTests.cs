using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Auth;
using API.Models.Common;
using Domain.AuditManagement.Aggregate;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Controllers
{
    /// <summary>"Forgot password", "reset password" and "change password" through the real pipeline.</summary>
    public class PasswordFlowTests : IntegrationTestBase
    {
        private const string OldPassword = "Password123!";
        private const string NewPassword = "Brand-new-456!";

        public PasswordFlowTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        private HttpClient Anonymous() => Factory.CreateClient();

        private static Task<HttpResponseMessage> Login(HttpClient client, string email, string password)
            => client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });

        private static Task<HttpResponseMessage> Refresh(HttpClient client, string refreshToken)
            => client.PostAsJsonAsync("/api/auth/refresh-token", new { RefreshToken = refreshToken });

        private static async Task<LoginResponseDto> TokensOf(HttpResponseMessage response)
            => (await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>())!.Data!;

        // ------------------------------------------------------------------ forgot + reset

        [Fact]
        public async Task ForgotAndReset_ReplaceThePassword_AndSignEveryDeviceOut()
        {
            await AuthenticateAsync("forgetful@example.com", OldPassword, "Forgetful", "0900000101");
            var client = Anonymous();
            var oldSession = await TokensOf(await Login(client, "forgetful@example.com", OldPassword));

            (await client.PostAsJsonAsync("/api/auth/forgot-password", new { Email = "forgetful@example.com" }))
                .StatusCode.Should().Be(HttpStatusCode.Accepted);
            var otp = TestEmailCapture.GetResetOtp("forgetful@example.com");

            var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
                new { Email = "forgetful@example.com", Otp = otp, NewPassword });
            reset.StatusCode.Should().Be(HttpStatusCode.OK);

            (await Login(client, "forgetful@example.com", OldPassword)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await Login(client, "forgetful@example.com", NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await Refresh(client, oldSession.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task TheResetCode_WorksOnlyOnce()
        {
            await AuthenticateAsync("once@example.com", OldPassword, "Once", "0900000102");
            var client = Anonymous();
            await client.PostAsJsonAsync("/api/auth/forgot-password", new { Email = "once@example.com" });
            var otp = TestEmailCapture.GetResetOtp("once@example.com");

            (await client.PostAsJsonAsync("/api/auth/reset-password", new { Email = "once@example.com", Otp = otp, NewPassword }))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            var again = await client.PostAsJsonAsync("/api/auth/reset-password",
                new { Email = "once@example.com", Otp = otp, NewPassword = "Another-one-789!" });
            again.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Login(client, "once@example.com", NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Forgot_GivesTheSameAnswerForAnUnknownAddress_AndSendsNothing()
        {
            var client = Anonymous();

            var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { Email = "nobody-here@example.com" });

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            var act = () => TestEmailCapture.GetResetOtp("nobody-here@example.com");
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public async Task Reset_WithAWrongCode_IsRefused_AndKeepsTheOldPassword()
        {
            await AuthenticateAsync("wrongcode@example.com", OldPassword, "Wrong Code", "0900000103");
            var client = Anonymous();
            await client.PostAsJsonAsync("/api/auth/forgot-password", new { Email = "wrongcode@example.com" });
            var otp = TestEmailCapture.GetResetOtp("wrongcode@example.com");
            var wrong = otp == "123456" ? "654321" : "123456";

            var response = await client.PostAsJsonAsync("/api/auth/reset-password",
                new { Email = "wrongcode@example.com", Otp = wrong, NewPassword });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Login(client, "wrongcode@example.com", OldPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Reset_WithAWeakPassword_IsRefusedByValidation()
        {
            var response = await Anonymous().PostAsJsonAsync("/api/auth/reset-password",
                new { Email = "someone@example.com", Otp = "123456", NewPassword = "weak" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Reset_GuessingCodes_LocksTheAddressOut()
        {
            await AuthenticateAsync("guessed@example.com", OldPassword, "Guessed", "0900000104");
            var client = Anonymous();
            await client.PostAsJsonAsync("/api/auth/forgot-password", new { Email = "guessed@example.com" });
            var otp = TestEmailCapture.GetResetOtp("guessed@example.com");
            var wrong = otp == "123456" ? "654321" : "123456";

            HttpStatusCode last = HttpStatusCode.OK;
            for (var i = 0; i < 8; i++)
            {
                var attempt = await client.PostAsJsonAsync("/api/auth/reset-password",
                    new { Email = "guessed@example.com", Otp = wrong, NewPassword });
                last = attempt.StatusCode;
            }

            // Even the right code is refused once the address is locked
            var right = await client.PostAsJsonAsync("/api/auth/reset-password",
                new { Email = "guessed@example.com", Otp = otp, NewPassword });

            (last == HttpStatusCode.TooManyRequests || right.StatusCode == HttpStatusCode.TooManyRequests).Should().BeTrue();
            right.StatusCode.Should().NotBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task TheAuditTrail_NeverContainsAResetCodeHash()
        {
            await AuthenticateAsync("audit-reset@example.com", OldPassword, "Audit Reset", "0900000105");
            var client = Anonymous();
            await client.PostAsJsonAsync("/api/auth/forgot-password", new { Email = "audit-reset@example.com" });
            var otp = TestEmailCapture.GetResetOtp("audit-reset@example.com");
            await client.PostAsJsonAsync("/api/auth/reset-password", new { Email = "audit-reset@example.com", Otp = otp, NewPassword });

            var logs = await ExecuteDbContextAsync(db => db.Set<AuditLog>().ToListAsync());

            logs.Select(l => (l.OldValue ?? string.Empty) + (l.NewValue ?? string.Empty))
                .Should().NotContain(v => v.Contains("PasswordResetOtp") || v.Contains("$2a$") || v.Contains("$2b$"));
        }

        // ------------------------------------------------------------------ change

        [Fact]
        public async Task Change_SignsOutOtherDevices_ButKeepsThisOneWorking()
        {
            await AuthenticateAsync("changer@example.com", OldPassword, "Changer", "0900000106");
            var other = await TokensOf(await Login(Anonymous(), "changer@example.com", OldPassword));

            var response = await Client.PostAsJsonAsync("/api/auth/change-password",
                new { CurrentPassword = OldPassword, NewPassword });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var mine = await TokensOf(response);

            (await Refresh(Anonymous(), other.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await Refresh(Anonymous(), mine.RefreshToken)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await Login(Anonymous(), "changer@example.com", OldPassword)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await Login(Anonymous(), "changer@example.com", NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Change_WithAWrongCurrentPassword_IsRefused()
        {
            await AuthenticateAsync("stolen@example.com", OldPassword, "Stolen Token", "0900000107");

            var response = await Client.PostAsJsonAsync("/api/auth/change-password",
                new { CurrentPassword = "Not-my-password1", NewPassword });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Login(Anonymous(), "stolen@example.com", OldPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Change_RequiresSignIn()
        {
            var response = await Anonymous().PostAsJsonAsync("/api/auth/change-password",
                new { CurrentPassword = OldPassword, NewPassword });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
