using System.Security.Claims;
using API.Helpers;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace UnitTests.Api
{
    public class TokenValidityTests
    {
        private static HttpContext ContextWith(long? iat)
        {
            var identity = new ClaimsIdentity("test");
            if (iat.HasValue)
                identity.AddClaim(new Claim("iat", iat.Value.ToString()));

            return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        }

        private static readonly DateTime Cutoff = new(2026, 10, 7, 12, 0, 30, 500, DateTimeKind.Utc);
        private static readonly long CutoffSeconds = new DateTimeOffset(Cutoff).ToUnixTimeSeconds();

        [Fact]
        public void WhenNoSessionWasEverEnded_EveryTokenIsAccepted()
        {
            UserActiveMiddleware.IsIssuedBefore(ContextWith(null), null).Should().BeFalse();
            UserActiveMiddleware.IsIssuedBefore(ContextWith(1), null).Should().BeFalse();
        }

        [Fact]
        public void ATokenIssuedBeforeTheCutoff_IsRejected()
        {
            UserActiveMiddleware.IsIssuedBefore(ContextWith(CutoffSeconds - 1), Cutoff).Should().BeTrue();
            UserActiveMiddleware.IsIssuedBefore(ContextWith(CutoffSeconds - 3600), Cutoff).Should().BeTrue();
        }

        [Fact]
        public void ATokenIssuedAtOrAfterTheCutoffSecond_IsAccepted_SoTheFreshTokenOfAPasswordChangeWorks()
        {
            UserActiveMiddleware.IsIssuedBefore(ContextWith(CutoffSeconds), Cutoff).Should().BeFalse();
            UserActiveMiddleware.IsIssuedBefore(ContextWith(CutoffSeconds + 5), Cutoff).Should().BeFalse();
        }

        [Fact]
        public void ATokenWithoutAnIssueTime_IsRejectedOnceSessionsWereEnded()
        {
            UserActiveMiddleware.IsIssuedBefore(ContextWith(null), Cutoff).Should().BeTrue();
        }

        [Fact]
        public void EndingAllSessions_SetsTheCutoff_ButEndingOneDeviceDoesNot()
        {
            var user = new User(Guid.NewGuid(), "u@example.com", "Secret123", "0901234567", "U", null, Role.Student, DateTime.UtcNow, true);
            user.IssueRefreshToken("device", TimeSpan.FromDays(7));

            user.RevokeRefreshToken("device");
            user.TokensValidFrom.Should().BeNull();

            var before = DateTime.UtcNow;
            user.RevokeAllRefreshTokens();
            user.TokensValidFrom.Should().BeOnOrAfter(before);
        }

        [Fact]
        public void ChangingOrResettingThePasswordOrErasing_AlsoSetsTheCutoff()
        {
            var changed = new User(Guid.NewGuid(), "a@example.com", "Secret123", "0901234567", "A", null, Role.Student, DateTime.UtcNow, true);
            changed.ChangePassword("Secret123", "Brand-new-456");
            changed.TokensValidFrom.Should().NotBeNull();

            var reset = new User(Guid.NewGuid(), "b@example.com", "Secret123", "0901234568", "B", null, Role.Student, DateTime.UtcNow, true);
            reset.ResetPassword(reset.GeneratePasswordResetOtp(TimeSpan.FromMinutes(5)), "Brand-new-456");
            reset.TokensValidFrom.Should().NotBeNull();

            var erased = new User(Guid.NewGuid(), "c@example.com", "Secret123", "0901234569", "C", null, Role.Student, DateTime.UtcNow, true);
            erased.Erase(DateTime.UtcNow);
            erased.TokensValidFrom.Should().NotBeNull();
        }
    }
}
