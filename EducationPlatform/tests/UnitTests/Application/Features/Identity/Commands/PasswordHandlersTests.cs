using Application.Exceptions;
using Application.Features.Identity.Commands.ChangePassword;
using Application.Features.Identity.Commands.ForgotPassword;
using Application.Features.Identity.Commands.ResetPassword;
using Application.Interface;
using Application.Results;
using Domain.Common.Interfaces;
using Domain.Exceptions;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Identity.Commands
{
    public class PasswordHandlersTests
    {
        private const string Email = "person@example.com";
        private const string OldPassword = "Secret123";
        private const string NewPassword = "Brand-new-456";

        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IUserRepository> _users = new();
        private readonly Mock<IEmailService> _email = new();
        private readonly Mock<ILoginAttemptTracker> _tracker = new();
        private readonly Mock<ICurrentUser> _currentUser = new();
        private readonly Mock<ITokenService> _tokens = new();

        public PasswordHandlersTests()
        {
            _unitOfWork.Setup(u => u.GetRepository<IUserRepository>()).Returns(_users.Object);
        }

        private User CreateUser(bool verified = true)
        {
            var user = new User(Guid.NewGuid(), Email, OldPassword, "0901234567", "Real Name", null, Role.Student, DateTime.UtcNow, verified);
            _users.Setup(r => r.GetUserByEmail(Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            _users.Setup(r => r.GetByIdWithSessions(user.UserID, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            return user;
        }

        // ------------------------------------------------------------------ forgot

        [Fact]
        public async Task Forgot_ForAVerifiedAccount_EmailsTheCodeAndStoresOnlyItsHash()
        {
            var user = CreateUser();
            string? sent = null;
            _email.Setup(e => e.SendPasswordResetEmailAsync(Email, It.IsAny<string>()))
                .Callback<string, string>((_, otp) => sent = otp)
                .Returns(Task.CompletedTask);

            await new ForgotPasswordCommandHandler(_unitOfWork.Object, _email.Object)
                .Handle(new ForgotPasswordCommand { Email = Email }, CancellationToken.None);

            sent.Should().MatchRegex(@"^\d{6}$");
            user.PasswordResetOtp.Should().NotBeNull().And.NotContain(sent!);
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task Forgot_ForAnUnknownAddress_DoesNothing_AndDoesNotSayso()
        {
            _users.Setup(r => r.GetUserByEmail(Email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var act = () => new ForgotPasswordCommandHandler(_unitOfWork.Object, _email.Object)
                .Handle(new ForgotPasswordCommand { Email = Email }, CancellationToken.None);

            await act.Should().NotThrowAsync();
            _email.Verify(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Forgot_ForAnUnverifiedAccount_DoesNothing()
        {
            CreateUser(verified: false);

            await new ForgotPasswordCommandHandler(_unitOfWork.Object, _email.Object)
                .Handle(new ForgotPasswordCommand { Email = Email }, CancellationToken.None);

            _email.Verify(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Forgot_TwiceInARow_SendsOnlyOneEmail()
        {
            CreateUser();
            var handler = new ForgotPasswordCommandHandler(_unitOfWork.Object, _email.Object);

            await handler.Handle(new ForgotPasswordCommand { Email = Email }, CancellationToken.None);
            await handler.Handle(new ForgotPasswordCommand { Email = Email }, CancellationToken.None);

            _email.Verify(e => e.SendPasswordResetEmailAsync(Email, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void ForgotValidator_RequiresAValidEmail()
        {
            var validator = new ForgotPasswordCommandValidator();

            validator.Validate(new ForgotPasswordCommand { Email = "" }).IsValid.Should().BeFalse();
            validator.Validate(new ForgotPasswordCommand { Email = "not-an-email" }).IsValid.Should().BeFalse();
            validator.Validate(new ForgotPasswordCommand { Email = Email }).IsValid.Should().BeTrue();
        }

        // ------------------------------------------------------------------ reset

        private ResetPasswordCommandHandler ResetHandler() => new(_unitOfWork.Object, _tracker.Object);

        [Fact]
        public async Task Reset_WithTheRightCode_ChangesThePassword_AndClearsTheCounters()
        {
            var user = CreateUser();
            var otp = user.GeneratePasswordResetOtp(TimeSpan.FromMinutes(10));

            await ResetHandler().Handle(new ResetPasswordCommand { Email = Email, Otp = otp, NewPassword = NewPassword }, CancellationToken.None);

            user.VerifyLogin(NewPassword).Should().BeTrue();
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Once);
            _tracker.Verify(t => t.Reset($"reset:{Email}"), Times.Once);
            _tracker.Verify(t => t.Reset(Email), Times.Once);
        }

        [Fact]
        public async Task Reset_WithAWrongCode_CountsAsAFailure()
        {
            var user = CreateUser();
            var otp = user.GeneratePasswordResetOtp(TimeSpan.FromMinutes(10));
            var wrong = otp == "123456" ? "654321" : "123456";

            var act = () => ResetHandler().Handle(new ResetPasswordCommand { Email = Email, Otp = wrong, NewPassword = NewPassword }, CancellationToken.None);

            await act.Should().ThrowAsync<DomainException>();
            _tracker.Verify(t => t.RegisterFailure($"reset:{Email}"), Times.Once);
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Reset_ForAnUnknownAddress_LooksLikeAWrongCode()
        {
            _users.Setup(r => r.GetUserByEmail(Email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var act = () => ResetHandler().Handle(new ResetPasswordCommand { Email = Email, Otp = "123456", NewPassword = NewPassword }, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Invalid or expired code.");
            _tracker.Verify(t => t.RegisterFailure($"reset:{Email}"), Times.Once);
        }

        [Fact]
        public async Task Reset_WhenLockedOut_IsRefusedBeforeLookingAnythingUp()
        {
            _tracker.Setup(t => t.IsLockedOut($"reset:{Email}")).Returns(true);

            var act = () => ResetHandler().Handle(new ResetPasswordCommand { Email = Email, Otp = "123456", NewPassword = NewPassword }, CancellationToken.None);

            await act.Should().ThrowAsync<TooManyRequestsException>();
            _users.Verify(r => r.GetUserByEmail(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void ResetValidator_ChecksTheCodeAndThePasswordPolicy()
        {
            var validator = new ResetPasswordCommandValidator();

            validator.Validate(new ResetPasswordCommand { Email = Email, Otp = "123456", NewPassword = NewPassword }).IsValid.Should().BeTrue();
            validator.Validate(new ResetPasswordCommand { Email = Email, Otp = "12345", NewPassword = NewPassword }).IsValid.Should().BeFalse();
            validator.Validate(new ResetPasswordCommand { Email = Email, Otp = "abcdef", NewPassword = NewPassword }).IsValid.Should().BeFalse();
            validator.Validate(new ResetPasswordCommand { Email = Email, Otp = "123456", NewPassword = "onlyletters" }).IsValid.Should().BeFalse();
        }

        // ------------------------------------------------------------------ change

        private ChangePasswordCommandHandler ChangeHandler() => new(_unitOfWork.Object, _currentUser.Object, _tokens.Object, _tracker.Object);

        [Fact]
        public async Task Change_WithTheRightPassword_SignsOutOtherDevices_AndHandsOutFreshTokens()
        {
            var user = CreateUser();
            user.IssueRefreshToken("other-device", TimeSpan.FromDays(7));
            _currentUser.Setup(c => c.Id).Returns(user.UserID);
            _tokens.Setup(t => t.GenerateRefreshToken()).Returns("fresh-refresh");
            _tokens.Setup(t => t.GenerateToken(user)).Returns("fresh-access");

            var result = await ChangeHandler().Handle(
                new ChangePasswordCommand { CurrentPassword = OldPassword, NewPassword = NewPassword }, CancellationToken.None);

            result.Token.Should().Be("fresh-access");
            result.RefreshToken.Should().Be("fresh-refresh");
            user.VerifyLogin(NewPassword).Should().BeTrue();
            user.CanRefresh("other-device").Should().BeFalse();
            user.CanRefresh("fresh-refresh").Should().BeTrue();
        }

        [Fact]
        public async Task Change_WithAWrongCurrentPassword_CountsAsAFailure()
        {
            var user = CreateUser();
            _currentUser.Setup(c => c.Id).Returns(user.UserID);

            var act = () => ChangeHandler().Handle(
                new ChangePasswordCommand { CurrentPassword = "Wrong-password1", NewPassword = NewPassword }, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>();
            _tracker.Verify(t => t.RegisterFailure($"password:{user.UserID}"), Times.Once);
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Change_WhenLockedOut_IsRefused()
        {
            var user = CreateUser();
            _currentUser.Setup(c => c.Id).Returns(user.UserID);
            _tracker.Setup(t => t.IsLockedOut($"password:{user.UserID}")).Returns(true);

            var act = () => ChangeHandler().Handle(
                new ChangePasswordCommand { CurrentPassword = OldPassword, NewPassword = NewPassword }, CancellationToken.None);

            await act.Should().ThrowAsync<TooManyRequestsException>();
        }

        [Fact]
        public async Task Change_WithoutSignIn_IsRefused()
        {
            _currentUser.Setup(c => c.Id).Returns((Guid?)null);

            var act = () => ChangeHandler().Handle(
                new ChangePasswordCommand { CurrentPassword = OldPassword, NewPassword = NewPassword }, CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
        }

        [Fact]
        public void ChangeValidator_RejectsAnUnchangedOrWeakPassword()
        {
            var validator = new ChangePasswordCommandValidator();

            validator.Validate(new ChangePasswordCommand { CurrentPassword = OldPassword, NewPassword = NewPassword }).IsValid.Should().BeTrue();
            validator.Validate(new ChangePasswordCommand { CurrentPassword = OldPassword, NewPassword = OldPassword }).IsValid.Should().BeFalse();
            validator.Validate(new ChangePasswordCommand { CurrentPassword = OldPassword, NewPassword = "short1" }).IsValid.Should().BeFalse();
            validator.Validate(new ChangePasswordCommand { CurrentPassword = "", NewPassword = NewPassword }).IsValid.Should().BeFalse();
        }
    }
}
