using Application.Exceptions;
using Application.Features.Identity.Commands.Login;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace UnitTests.Application.Features.Identity.Commands.Login
{
    public class LoginCommandTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly Mock<ILoginAttemptTracker> _mockAttemptTracker;
        private readonly LoginCommandHandler _handler;

        public LoginCommandTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IUserRepository>())
                .Returns(_mockUserRepository.Object);

            _mockTokenService = new Mock<ITokenService>();

            _mockTokenService
                .Setup(t => t.GenerateToken(It.IsAny<User>()))
                .Returns("mocked-jwt-token");

            _mockTokenService
                .Setup(t => t.GenerateRefreshToken())
                .Returns("mocked-refresh-token");

            _mockAttemptTracker = new Mock<ILoginAttemptTracker>();

            _handler = new LoginCommandHandler(_mockUnitOfWork.Object, _mockTokenService.Object, _mockAttemptTracker.Object);
        }

        [Fact]
        public async Task Handle_DeactivatedAccount_ShouldBeForbiddenAndGetNoTokens()
        {
            var user = new User(Guid.NewGuid(), "locked@gmail.com", "password123", "0123456789", "Locked", null, Role.Student, DateTime.UtcNow, isVerified: true);
            user.Deactivate();
            _mockUserRepository.Setup(r => r.GetUserByEmail("locked@gmail.com")).ReturnsAsync(user);

            Func<Task> act = async () => await _handler.Handle(
                new LoginCommand { Email = "locked@gmail.com", Password = "password123" }, CancellationToken.None);

            await act.Should().ThrowAsync<ForbiddenException>();
            _mockTokenService.Verify(t => t.GenerateToken(It.IsAny<User>()), Times.Never);
            user.RefreshSessions.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_Success_ShouldReturnTokenAndSaveRefreshToken()
        {
            // Arrange
            var email = "test@gmail.com";
            var password = "password123";

            var user = new User(
                Guid.NewGuid(),
                email,
                password,
                "0123456789",
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: true
            );

            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync(user);

            var command = new LoginCommand
            {
                Email = email,
                Password = password
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Token.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBeNullOrEmpty();

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            user.RefreshSessions.Should().ContainSingle(x => x.IsActive);
            _mockAttemptTracker.Verify(t => t.Reset(email), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_ShouldThrowAuthenticateException()
        {
            // Arrange
            var email = "notfound@gmail.com";
            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync((User?)null);

            var command = new LoginCommand
            {
                Email = email,
                Password = "password123"
            };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("Invalid credentials.");
        }

        [Fact]
        public async Task Handle_InvalidPassword_ShouldThrowAuthenticateException()
        {
            // Arrange
            var email = "test@gmail.com";
            var user = new User(
                Guid.NewGuid(),
                email,
                "correctpassword1",
                "0123456789",
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: true
            );

            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync(user);

            var command = new LoginCommand
            {
                Email = email,
                Password = "wrongpassword"
            };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("Invalid credentials.");
        }

        [Fact]
        public async Task Handle_EmailNotVerified_ShouldThrowAuthenticateException()
        {
            // Arrange
            var email = "test@gmail.com";
            var password = "password123";
            var user = new User(
                Guid.NewGuid(),
                email,
                password,
                "0123456789",
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: false // email is not verified
            );

            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync(user);

            var command = new LoginCommand
            {
                Email = email,
                Password = password
            };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("Invalid credentials.");
        }

        [Fact]
        public async Task Handle_SecondLogin_ShouldKeepTheFirstDeviceSignedIn()
        {
            var email = "test@gmail.com";
            var user = new User(Guid.NewGuid(), email, "password123", "0123456789", "Test User", null, Role.Student, DateTime.UtcNow, isVerified: true);
            _mockUserRepository.Setup(r => r.GetUserByEmail(email)).ReturnsAsync(user);

            var refreshTokens = new Queue<string>(new[] { "token-device-1", "token-device-2" });
            _mockTokenService.Setup(t => t.GenerateRefreshToken()).Returns(() => refreshTokens.Dequeue());

            var command = new LoginCommand { Email = email, Password = "password123" };
            await _handler.Handle(command, CancellationToken.None);
            await _handler.Handle(command, CancellationToken.None);

            user.CanRefresh("token-device-1").Should().BeTrue();
            user.CanRefresh("token-device-2").Should().BeTrue();
        }

        [Fact]
        public async Task Handle_WrongPassword_ShouldRegisterFailure()
        {
            var email = "test@gmail.com";
            var user = new User(Guid.NewGuid(), email, "password123", "0123456789", "Test User", null, Role.Student, DateTime.UtcNow, isVerified: true);
            _mockUserRepository.Setup(r => r.GetUserByEmail(email)).ReturnsAsync(user);

            Func<Task> act = async () => await _handler.Handle(new LoginCommand { Email = email, Password = "wrong" }, CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
            _mockAttemptTracker.Verify(t => t.RegisterFailure(email), Times.Once);
            _mockAttemptTracker.Verify(t => t.Reset(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Handle_LockedOutAccount_ShouldThrowTooManyRequestsWithoutHittingTheDatabase()
        {
            var email = "test@gmail.com";
            _mockAttemptTracker.Setup(t => t.IsLockedOut(email)).Returns(true);

            Func<Task> act = async () => await _handler.Handle(new LoginCommand { Email = email, Password = "password123" }, CancellationToken.None);

            await act.Should().ThrowAsync<TooManyRequestsException>();
            _mockUserRepository.Verify(r => r.GetUserByEmail(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}