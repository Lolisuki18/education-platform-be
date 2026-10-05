using Application.Exceptions;
using Application.Features.Identity.Commands.RefreshToken;
using Application.Interface;
using Application.Results;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using FluentAssertions;
using Moq;
using System;
using System.Reflection;
using Domain.IdentityManagement.Entity;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace UnitTests.Application.Features.Identity.Commands.RefreshToken
{
    public class RefreshTokenCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly RefreshTokenCommandHandler _handler;

        public RefreshTokenCommandHandlerTests()
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

            _handler = new RefreshTokenCommandHandler(_mockUnitOfWork.Object, _mockTokenService.Object);
        }

        private static User CreateUser(bool isActive = true)
        {
            var user = new User(
                Guid.NewGuid(),
                "test@gmail.com",
                "password123",
                "0123456789",
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: true
            );

            if (!isActive)
                user.Deactivate();

            return user;
        }

        private static void SetPrivate(object target, string property, object? value)
        {
            typeof(RefreshSession)
                .GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(target, value);
        }

        [Fact]
        public async Task Handle_InvalidRefreshToken_ShouldThrowAuthenticateException()
        {
            // Arrange
            var refreshToken = "invalid_token";
            _mockUserRepository
                .Setup(r => r.GetByRefreshToken(refreshToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync((User?)null);

            var command = new RefreshTokenCommand { RefreshToken = refreshToken };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("Invalid refresh token.");
        }

        [Fact]
        public async Task Handle_ExpiredRefreshToken_ShouldThrowAuthenticateException()
        {
            // Arrange
            var tokenString = "expired_token";
            var user = CreateUser();
            var session = user.IssueRefreshToken(tokenString, TimeSpan.FromMinutes(10));
            SetPrivate(session, nameof(RefreshSession.ExpiresAt), DateTime.UtcNow.AddMinutes(-5));

            _mockUserRepository
                .Setup(r => r.GetByRefreshToken(tokenString, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            var command = new RefreshTokenCommand { RefreshToken = tokenString };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("Refresh token has expired.");
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldRotateTheSessionAndReturnNewTokens()
        {
            // Arrange
            var tokenString = "valid_token";
            var user = CreateUser();
            user.IssueRefreshToken(tokenString, TimeSpan.FromMinutes(10));

            _mockUserRepository
                .Setup(r => r.GetByRefreshToken(tokenString, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            var command = new RefreshTokenCommand { RefreshToken = tokenString };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Token.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().Be("mocked-refresh-token");
            result.RefreshToken.Should().NotBe(tokenString); // a new token replaces the old one

            user.CanRefresh(tokenString).Should().BeFalse();
            user.CanRefresh("mocked-refresh-token").Should().BeTrue();

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(null), Times.Once);
        }

        [Fact]
        public async Task Handle_ReplayedToken_ShouldRevokeEverySessionAndPersistIt()
        {
            // Arrange: the token was rotated long ago, then someone presents it again
            var user = CreateUser();
            var stolen = user.IssueRefreshToken("stolen_token", TimeSpan.FromDays(7));
            var otherDevice = user.IssueRefreshToken("other_device_token", TimeSpan.FromDays(7));
            stolen.Revoke();
            SetPrivate(stolen, nameof(RefreshSession.RevokedAt), DateTime.UtcNow.AddMinutes(-5));

            _mockUserRepository
                .Setup(r => r.GetByRefreshToken("stolen_token", It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            // Act
            Func<Task> act = async () => await _handler.Handle(new RefreshTokenCommand { RefreshToken = "stolen_token" }, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>().WithMessage("*already used*");
            otherDevice.IsRevoked.Should().BeTrue();
            _mockUnitOfWork.Verify(u => u.CommitAsync(null), Times.Once);
        }

        [Fact]
        public async Task Handle_RetryRightAfterRotation_ShouldBeRejectedWithoutRevokingOtherSessions()
        {
            // Arrange: a flaky client repeats the request a moment after the token was rotated
            var user = CreateUser();
            user.IssueRefreshToken("old_token", TimeSpan.FromDays(7));
            user.RotateRefreshToken("old_token", "new_token", TimeSpan.FromDays(7));

            _mockUserRepository
                .Setup(r => r.GetByRefreshToken("old_token", It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            // Act
            Func<Task> act = async () => await _handler.Handle(new RefreshTokenCommand { RefreshToken = "old_token" }, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>().WithMessage("Invalid refresh token.");
            user.CanRefresh("new_token").Should().BeTrue();
            _mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Handle_InactiveUser_ShouldThrowForbidden()
        {
            var user = CreateUser(isActive: false);
            user.IssueRefreshToken("valid_token", TimeSpan.FromDays(7));

            _mockUserRepository
                .Setup(r => r.GetByRefreshToken("valid_token", It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

            Func<Task> act = async () => await _handler.Handle(new RefreshTokenCommand { RefreshToken = "valid_token" }, CancellationToken.None);

            await act.Should().ThrowAsync<ForbiddenException>();
        }
    }
}
