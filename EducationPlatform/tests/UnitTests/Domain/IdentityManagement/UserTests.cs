using Domain.Exceptions;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Entity;
using Domain.IdentityManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using FluentAssertions;
using System;
using System.Reflection;
using Xunit;

namespace UnitTests.DomainTests.IdentityManagement
{
    public class UserTests
    {
        [Fact]
        public void Constructor_EmptyUserId_ShouldThrowDomainException()
        {
            Action act = () => new User(
                Guid.Empty,
                "test@gmail.com",
                "password123",
                "0123456789",
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow
            );

            act.Should().Throw<DomainException>()
                .WithMessage("User ID cannot be empty");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidEmail_ShouldThrowDomainException(string? email)
        {
            Action act = () => new User(
                Guid.NewGuid(),
                email!,
                "password123",
                "0123456789",
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow
            );

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Constructor_EmailWithoutAtSign_ShouldThrowDomainException()
        {
            Action act = () => new User(
                Guid.NewGuid(),
                "testgmail.com",
                "password123",
                "0123456789",
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow
            );

            act.Should().Throw<DomainException>()
                .WithMessage("Invalid email format");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidPhone_ShouldThrowDomainException(string? phone)
        {
            Action act = () => new User(
                Guid.NewGuid(),
                "test@gmail.com",
                "password123",
                phone!,
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow
            );

            act.Should().Throw<DomainException>();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_InvalidName_ShouldThrowDomainException(string? name)
        {
            Action act = () => new User(
                Guid.NewGuid(),
                "test@gmail.com",
                "password123",
                "0123456789",
                name!,
                null,
                Role.Student,
                DateTime.UtcNow
            );

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Constructor_InvalidRole_ShouldThrowDomainException()
        {
            Action act = () => new User(
                Guid.NewGuid(),
                "test@gmail.com",
                "password123",
                "0123456789",
                "Test User",
                null,
                (Role)99, // invalid role
                DateTime.UtcNow
            );

            act.Should().Throw<DomainException>()
                .WithMessage("Invalid role");
        }

        [Fact]
        public void Constructor_ValidArguments_ShouldCreateUserSuccessfully()
        {
            var userId = Guid.NewGuid();
            var email = "test@gmail.com";
            var phone = "0123456789";
            var name = "Test User";
            var bio = "Student Bio";
            var createdAt = DateTime.UtcNow;

            var user = new User(userId, email, "password123", phone, name, bio, Role.Student, createdAt, isVerified: true);

            user.UserID.Should().Be(userId);
            user.Email.Should().Be(email);
            user.Phone.Should().Be(phone);
            user.Name.Should().Be(name);
            user.Bio.Should().Be(bio);
            user.Role.Should().Be(Role.Student);
            user.IsVerified.Should().BeTrue();
            user.IsActive.Should().BeTrue();
            user.CreatedAt.Should().Be(createdAt);
        }

        [Fact]
        public void Password_CreateShortPassword_ShouldThrowDomainException()
        {
            Action act = () => Password.Create("123");
            act.Should().Throw<DomainException>()
                .WithMessage("Password must be at least 8 characters");
        }

        [Fact]
        public void Password_Verify_ShouldReturnTrueForCorrectPassword()
        {
            var plainText = "password123";
            var password = Password.Create(plainText);

            password.Verify(plainText).Should().BeTrue();
            password.Verify("wrongpassword").Should().BeFalse();
        }

        [Fact]
        public void GenerateEmailOtp_ShouldSetSixDigitOtpAndExpiration()
        {
            var user = CreateTestUser(isVerified: false);
            var lifetime = TimeSpan.FromMinutes(5);

            var otp = user.GenerateEmailOtp(lifetime);

            otp.Length.Should().Be(6);
            int.TryParse(otp, out _).Should().BeTrue();

            // The database never sees the code itself, only a hash of it
            user.EmailOtp.Should().NotBeNullOrEmpty();
            user.EmailOtp.Should().NotBe(otp);
            user.EmailOtpExpiresAt.Should().NotBeNull();
            user.EmailOtpExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.Add(lifetime), TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void VerifyEmail_AlreadyVerified_ShouldThrowDomainException()
        {
            var user = CreateTestUser(isVerified: true);
            Action act = () => user.VerifyEmail("123456");

            act.Should().Throw<DomainException>()
                .WithMessage("Email already verified.");
        }

        [Fact]
        public void VerifyEmail_OtpNotGenerated_ShouldThrowDomainException()
        {
            var user = CreateTestUser(isVerified: false);
            Action act = () => user.VerifyEmail("123456");

            act.Should().Throw<DomainException>()
                .WithMessage("OTP not generated.");
        }

        [Fact]
        public void VerifyEmail_OtpExpired_ShouldThrowDomainException()
        {
            var user = CreateTestUser(isVerified: false);
            var otp = user.GenerateEmailOtp(TimeSpan.FromMinutes(-5)); // expired

            Action act = () => user.VerifyEmail(otp);

            act.Should().Throw<DomainException>()
                .WithMessage("OTP has expired.");
        }

        [Fact]
        public void VerifyEmail_InvalidOtp_ShouldThrowDomainException()
        {
            var user = CreateTestUser(isVerified: false);
            user.GenerateEmailOtp(TimeSpan.FromMinutes(5));

            Action act = () => user.VerifyEmail("wrong_otp");

            act.Should().Throw<DomainException>()
                .WithMessage("Invalid OTP.");
        }

        [Fact]
        public void VerifyEmail_CorrectOtp_ShouldVerifyUserAndResetOtpProperties()
        {
            var user = CreateTestUser(isVerified: false);
            var otp = user.GenerateEmailOtp(TimeSpan.FromMinutes(5));

            user.VerifyEmail(otp);

            user.IsVerified.Should().BeTrue();
            user.EmailOtp.Should().BeNull();
            user.EmailOtpExpiresAt.Should().BeNull();
        }

        [Fact]
        public void VerifyLogin_ShouldSucceedOnlyIfVerifiedAndPasswordCorrect()
        {
            // Case 1: Unverified user
            var unverifiedUser = CreateTestUser(isVerified: false);
            unverifiedUser.VerifyLogin("password123").Should().BeFalse();

            // Case 2: Verified user, correct password
            var verifiedUser = CreateTestUser(isVerified: true);
            verifiedUser.VerifyLogin("password123").Should().BeTrue();

            // Case 3: Verified user, incorrect password
            verifiedUser.VerifyLogin("wrongpassword").Should().BeFalse();
        }

        [Fact]
        public void IssueRefreshToken_ShouldAddAnActiveSession()
        {
            var user = CreateTestUser(isVerified: true);
            var token = "raw_refresh_token";

            user.IssueRefreshToken(token, TimeSpan.FromMinutes(10));

            user.RefreshSessions.Should().ContainSingle();
            user.CanRefresh(token).Should().BeTrue();
        }

        [Fact]
        public void IssueRefreshToken_ForSeveralDevices_ShouldKeepEverySessionActive()
        {
            var user = CreateTestUser(isVerified: true);

            user.IssueRefreshToken("phone", TimeSpan.FromMinutes(10));
            user.IssueRefreshToken("laptop", TimeSpan.FromMinutes(10));

            user.CanRefresh("phone").Should().BeTrue();
            user.CanRefresh("laptop").Should().BeTrue();
        }

        [Fact]
        public void IssueRefreshToken_BeyondTheLimit_ShouldRevokeTheOldestSessions()
        {
            var user = CreateTestUser(isVerified: true);

            for (var i = 0; i < User.MaxActiveSessions + 2; i++)
                user.IssueRefreshToken($"token-{i}", TimeSpan.FromMinutes(10));

            user.RefreshSessions.Count(s => s.IsActive).Should().Be(User.MaxActiveSessions);
            user.CanRefresh("token-0").Should().BeFalse();
            user.CanRefresh($"token-{User.MaxActiveSessions + 1}").Should().BeTrue();
        }

        [Fact]
        public void CanRefresh_ExpiredToken_ShouldReturnFalse()
        {
            var user = CreateTestUser(isVerified: true);
            var session = user.IssueRefreshToken("token_string", TimeSpan.FromMinutes(10));

            // Set ExpiresAt to past via reflection
            typeof(RefreshSession)
                .GetField("<ExpiresAt>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(session, DateTime.UtcNow.AddMinutes(-5));

            user.CanRefresh("token_string").Should().BeFalse();
        }

        [Fact]
        public void RotateRefreshToken_ShouldReplaceTheOldTokenWithTheNewOne()
        {
            var user = CreateTestUser(isVerified: true);
            user.IssueRefreshToken("old", TimeSpan.FromMinutes(10));

            var result = user.RotateRefreshToken("old", "new", TimeSpan.FromMinutes(10));

            result.Should().Be(RefreshResult.Rotated);
            user.CanRefresh("old").Should().BeFalse();
            user.CanRefresh("new").Should().BeTrue();
        }

        [Fact]
        public void RotateRefreshToken_UnknownToken_ShouldReturnInvalid()
        {
            var user = CreateTestUser(isVerified: true);

            user.RotateRefreshToken("nope", "new", TimeSpan.FromMinutes(10)).Should().Be(RefreshResult.Invalid);
        }

        [Fact]
        public void RevokeRefreshToken_ShouldOnlyRevokeThatDevice()
        {
            var user = CreateTestUser(isVerified: true);
            user.IssueRefreshToken("phone", TimeSpan.FromMinutes(10));
            user.IssueRefreshToken("laptop", TimeSpan.FromMinutes(10));

            user.RevokeRefreshToken("phone").Should().BeTrue();

            user.CanRefresh("phone").Should().BeFalse();
            user.CanRefresh("laptop").Should().BeTrue();
        }

        [Fact]
        public void RevokeAllRefreshTokens_ShouldSignEveryDeviceOut()
        {
            var user = CreateTestUser(isVerified: true);
            user.IssueRefreshToken("phone", TimeSpan.FromMinutes(10));
            user.IssueRefreshToken("laptop", TimeSpan.FromMinutes(10));

            user.RevokeAllRefreshTokens();

            user.RefreshSessions.Should().OnlyContain(s => s.IsRevoked);
        }

        [Fact]
        public void UpdateProfile_ShouldOnlyUpdateProvidedNonNullOrWhitespaceArguments()
        {
            var user = CreateTestUser(isVerified: true);

            // Update with valid new values
            user.UpdateProfile("New Name", "0987654321", "New Bio");
            user.Name.Should().Be("New Name");
            user.Phone.Should().Be("0987654321");
            user.Bio.Should().Be("New Bio");

            // Update with null or whitespace -> should keep existing values
            user.UpdateProfile(null, "", "   ");
            user.Name.Should().Be("New Name");
            user.Phone.Should().Be("0987654321");
            user.Bio.Should().Be("New Bio");
        }

        private User CreateTestUser(bool isVerified)
        {
            return new User(
                Guid.NewGuid(),
                "test@gmail.com",
                "password123",
                "0123456789",
                "Test User",
                "Bio text",
                Role.Student,
                DateTime.UtcNow,
                isVerified
            );
        }

        [Fact]
        public void GenerateEmailOtp_ShouldProduceDifferentHashesForTheSameCodeOfDifferentUsers()
        {
            var a = CreateTestUser(isVerified: false);
            var b = CreateTestUser(isVerified: false);

            a.GenerateEmailOtp(TimeSpan.FromMinutes(5));
            b.GenerateEmailOtp(TimeSpan.FromMinutes(5));

            // A user can only verify with a code issued to them
            Action act = () => b.VerifyEmail("123456");
            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void CanRequestNewOtp_ShouldBeFalseDuringTheCooldownAndTrueAfterwards()
        {
            var lifetime = TimeSpan.FromMinutes(5);
            var cooldown = TimeSpan.FromSeconds(60);
            var user = CreateTestUser(isVerified: false);

            user.CanRequestNewOtp(lifetime, cooldown).Should().BeTrue(); // nothing sent yet

            user.GenerateEmailOtp(lifetime);
            user.CanRequestNewOtp(lifetime, cooldown).Should().BeFalse(); // just sent

            user.GenerateEmailOtp(lifetime - TimeSpan.FromSeconds(90)); // pretend it was sent 90 seconds ago
            user.CanRequestNewOtp(lifetime, cooldown).Should().BeTrue();
        }

        [Fact]
        public void ReissueRegistration_ShouldReplacePasswordAndProfileOfAnUnverifiedUser()
        {
            var user = CreateTestUser(isVerified: false);

            user.ReissueRegistration("NewPassword1", "0911111111", "Real Owner", "bio", Role.Teacher);

            user.Password.Verify("NewPassword1").Should().BeTrue();
            user.Password.Verify("password123").Should().BeFalse();
            user.Phone.Should().Be("0911111111");
            user.Name.Should().Be("Real Owner");
            user.Role.Should().Be(Role.Teacher);
        }

        [Fact]
        public void ReissueRegistration_ForAVerifiedUser_ShouldThrow()
        {
            var user = CreateTestUser(isVerified: true);

            Action act = () => user.ReissueRegistration("NewPassword1", "0911111111", "Name", null, Role.Student);

            act.Should().Throw<DomainException>().WithMessage("Email already verified.");
        }

        [Theory]
        [InlineData("short1", "at least 8 characters")]
        [InlineData("onlyletters", "letter and one digit")]
        [InlineData("12345678", "letter and one digit")]
        public void Password_Create_ShouldEnforceThePolicy(string password, string expectedMessagePart)
        {
            Action act = () => Domain.IdentityManagement.ValueObject.Password.Create(password);

            act.Should().Throw<DomainException>().WithMessage($"*{expectedMessagePart}*");
        }

        [Fact]
        public void Password_Create_ShouldRejectPasswordsLongerThanBCryptCanUse()
        {
            var tooLong = new string('a', 70) + "12345"; // 75 bytes

            Action act = () => Domain.IdentityManagement.ValueObject.Password.Create(tooLong);

            act.Should().Throw<DomainException>().WithMessage("*72 bytes*");
        }

        [Fact]
        public void Password_Create_ShouldCountBytesNotCharacters()
        {
            // 40 x 2-byte characters + digits = 84 bytes although only 43 characters
            var multiByte = new string('é', 40) + "123";

            Action act = () => Domain.IdentityManagement.ValueObject.Password.Create(multiByte);

            act.Should().Throw<DomainException>().WithMessage("*72 bytes*");
        }
    }
}
