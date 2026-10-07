using Domain.Exceptions;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using FluentAssertions;
using Xunit;

namespace UnitTests.DomainTests.IdentityManagement
{
    public class UserPasswordTests
    {
        private const string OldPassword = "Secret123";
        private const string NewPassword = "Brand-new-456";
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        private static User CreateUser(bool verified = true)
            => new(Guid.NewGuid(), "person@example.com", OldPassword, "0901234567", "Real Name", null, Role.Student, DateTime.UtcNow, verified);

        // ------------------------------------------------------------------ reset

        [Fact]
        public void GeneratePasswordResetOtp_ReturnsASixDigitCode_AndStoresOnlyAHash()
        {
            var user = CreateUser();

            var otp = user.GeneratePasswordResetOtp(Lifetime);

            otp.Should().MatchRegex(@"^\d{6}$");
            user.PasswordResetOtp.Should().NotBeNull().And.NotContain(otp);
            user.PasswordResetOtpExpiresAt.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public void ResetPassword_WithTheEmailedCode_ChangesThePassword_AndRevokesEverySession()
        {
            var user = CreateUser();
            user.IssueRefreshToken("device-one", TimeSpan.FromDays(7));
            user.IssueRefreshToken("device-two", TimeSpan.FromDays(7));
            var otp = user.GeneratePasswordResetOtp(Lifetime);

            user.ResetPassword(otp, NewPassword);

            user.VerifyLogin(NewPassword).Should().BeTrue();
            user.VerifyLogin(OldPassword).Should().BeFalse();
            user.CanRefresh("device-one").Should().BeFalse();
            user.CanRefresh("device-two").Should().BeFalse();
        }

        [Fact]
        public void ResetPassword_CodeCanBeUsedOnlyOnce()
        {
            var user = CreateUser();
            var otp = user.GeneratePasswordResetOtp(Lifetime);
            user.ResetPassword(otp, NewPassword);

            var act = () => user.ResetPassword(otp, "Another-one-789");

            act.Should().Throw<DomainException>().WithMessage("Invalid or expired code.");
            user.VerifyLogin(NewPassword).Should().BeTrue();
        }

        [Fact]
        public void ResetPassword_WithAWrongCode_ChangesNothing()
        {
            var user = CreateUser();
            var otp = user.GeneratePasswordResetOtp(Lifetime);
            var wrong = otp == "123456" ? "654321" : "123456";

            var act = () => user.ResetPassword(wrong, NewPassword);

            act.Should().Throw<DomainException>().WithMessage("Invalid or expired code.");
            user.VerifyLogin(OldPassword).Should().BeTrue();
            user.PasswordResetOtp.Should().NotBeNull();
        }

        [Fact]
        public void ResetPassword_WithAnExpiredCode_IsRefused()
        {
            var user = CreateUser();
            var otp = user.GeneratePasswordResetOtp(TimeSpan.FromSeconds(-1));

            var act = () => user.ResetPassword(otp, NewPassword);

            act.Should().Throw<DomainException>().WithMessage("Invalid or expired code.");
        }

        [Fact]
        public void ResetPassword_WithoutARequestedCode_IsRefusedWithTheSameMessage()
        {
            var user = CreateUser();

            var act = () => user.ResetPassword("123456", NewPassword);

            act.Should().Throw<DomainException>().WithMessage("Invalid or expired code.");
        }

        [Fact]
        public void ResetPassword_AWeakNewPassword_IsRefused_AndTheCodeIsNotSpentOnIt()
        {
            var user = CreateUser();
            var otp = user.GeneratePasswordResetOtp(Lifetime);

            var act = () => user.ResetPassword(otp, "short");

            act.Should().Throw<DomainException>();
            user.VerifyLogin(OldPassword).Should().BeTrue();
            user.PasswordResetOtp.Should().NotBeNull();
        }

        [Fact]
        public void TheVerificationCode_IsNotAResetCode()
        {
            var user = CreateUser(verified: false);
            var emailOtp = user.GenerateEmailOtp(Lifetime);
            user.VerifyEmail(emailOtp);

            var act = () => user.ResetPassword(emailOtp, NewPassword);

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void AnUnverifiedOrDeactivatedOrErasedAccount_CannotReset()
        {
            CreateUser(verified: false).CanResetPassword.Should().BeFalse();

            var deactivated = CreateUser();
            deactivated.Deactivate();
            deactivated.CanResetPassword.Should().BeFalse();
            ((Action)(() => deactivated.GeneratePasswordResetOtp(Lifetime))).Should().Throw<DomainException>();

            var erased = CreateUser();
            erased.Erase(DateTime.UtcNow);
            erased.CanResetPassword.Should().BeFalse();
        }

        [Fact]
        public void CanRequestPasswordReset_WaitsForTheCooldown()
        {
            var user = CreateUser();
            user.CanRequestPasswordReset(Lifetime, TimeSpan.FromSeconds(60)).Should().BeTrue();

            user.GeneratePasswordResetOtp(Lifetime);

            user.CanRequestPasswordReset(Lifetime, TimeSpan.FromSeconds(60)).Should().BeFalse();
            user.CanRequestPasswordReset(Lifetime, TimeSpan.Zero).Should().BeTrue();
        }

        [Fact]
        public void Erase_DropsAPendingResetCode()
        {
            var user = CreateUser();
            user.GeneratePasswordResetOtp(Lifetime);

            user.Erase(DateTime.UtcNow);

            user.PasswordResetOtp.Should().BeNull();
            user.PasswordResetOtpExpiresAt.Should().BeNull();
        }

        // ------------------------------------------------------------------ change

        [Fact]
        public void ChangePassword_WithTheRightCurrentPassword_ChangesIt_AndRevokesEverySession()
        {
            var user = CreateUser();
            user.IssueRefreshToken("device-one", TimeSpan.FromDays(7));
            user.GeneratePasswordResetOtp(Lifetime);

            user.ChangePassword(OldPassword, NewPassword);

            user.VerifyLogin(NewPassword).Should().BeTrue();
            user.VerifyLogin(OldPassword).Should().BeFalse();
            user.CanRefresh("device-one").Should().BeFalse();
            user.PasswordResetOtp.Should().BeNull("a code requested before the change must not work afterwards");
        }

        [Fact]
        public void ChangePassword_WithAWrongCurrentPassword_Throws()
        {
            var user = CreateUser();

            var act = () => user.ChangePassword("Wrong-password1", NewPassword);

            act.Should().Throw<DomainException>();
            user.VerifyLogin(OldPassword).Should().BeTrue();
        }

        [Fact]
        public void ChangePassword_ToTheSamePassword_Throws()
        {
            var user = CreateUser();

            var act = () => user.ChangePassword(OldPassword, OldPassword);

            act.Should().Throw<DomainException>();
        }
    }
}
