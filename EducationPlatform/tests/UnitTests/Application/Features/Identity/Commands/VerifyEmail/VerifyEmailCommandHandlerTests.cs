using Application.BusinessException;
using Application.Features.Identity.Commands.VerifyEmail;
using Domain.Common.Interfaces;
using Domain.DomainExceptions;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using FluentAssertions;
using MediatR;
using Moq;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Identity.Commands.VerifyEmail
{
    public class VerifyEmailCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly VerifyEmailCommandHandler _handler;

        public VerifyEmailCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IUserRepository>())
                .Returns(_mockUserRepository.Object);

            _handler = new VerifyEmailCommandHandler(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task Handle_UserNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var email = "notfound@gmail.com";
            var otp = "123456";
            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync((User?)null);

            var command = new VerifyEmailCommand { Email = email, Otp = otp };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFound>()
                .WithMessage("User not found.");
        }

        [Fact]
        public async Task Handle_EmailAlreadyVerified_ShouldThrowDomainException()
        {
            // Arrange
            var email = "verified@gmail.com";
            var otp = "123456";
            var user = new User(
                Guid.NewGuid(),
                email,
                "password123",
                "0123456789",
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: true
            );

            SetPrivateProperty(user, nameof(User.EmailOtp), otp);
            SetPrivateProperty(user, nameof(User.EmailOtpExpiresAt), DateTime.UtcNow.AddMinutes(5));

            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync(user);

            var command = new VerifyEmailCommand { Email = email, Otp = otp };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("Email already verified.");
        }

        [Fact]
        public async Task Handle_OtpExpired_ShouldThrowDomainException()
        {
            // Arrange
            var email = "test@gmail.com";
            var user = new User(
                Guid.NewGuid(),
                email,
                "password123",
                "0123456789",
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: false
            );

            user.GenerateEmailOtp(TimeSpan.FromMinutes(-5));
            var expiredOtp = user.EmailOtp!;

            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync(user);

            var command = new VerifyEmailCommand { Email = email, Otp = expiredOtp };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("OTP has expired.");
        }

        [Fact]
        public async Task Handle_ValidOtp_ShouldVerifyEmailAndCommitSuccessfully()
        {
            // Arrange
            var email = "test@gmail.com";
            var user = new User(
                Guid.NewGuid(),
                email,
                "password123",
                "0123456789",
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: false
            );

            user.GenerateEmailOtp(TimeSpan.FromMinutes(5));
            var validOtp = user.EmailOtp!;

            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync(user);

            var command = new VerifyEmailCommand { Email = email, Otp = validOtp };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().Be(Unit.Value);
            user.IsVerified.Should().BeTrue();
            user.EmailOtp.Should().BeNull();
            user.EmailOtpExpiresAt.Should().BeNull();

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<string>()), Times.Once);
        }

        private void SetPrivateProperty(object target, string propertyName, object value)
        {
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(target, value);
        }
    }
}
