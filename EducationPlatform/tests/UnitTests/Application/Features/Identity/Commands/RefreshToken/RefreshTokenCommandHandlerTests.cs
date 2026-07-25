using Application.BusinessException;
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

        [Fact]
        public async Task Handle_InvalidRefreshToken_ShouldThrowAuthenticateException()
        {
            // Arrange
            var refreshToken = "invalid_token";
            _mockUserRepository
                .Setup(r => r.GetByRefreshToken(refreshToken))
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

            // Tạo refresh token hợp lệ trước
            user.IssueRefreshToken(tokenString, TimeSpan.FromMinutes(10));

            // Dùng reflection để sửa ExpiresAt của RefreshToken thành quá khứ (đã hết hạn)
            var refreshTokenField = typeof(Domain.IdentityManagement.ValueObject.RefreshToken)
                .GetField("<ExpiresAt>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (refreshTokenField != null && user.RefreshToken != null)
            {
                refreshTokenField.SetValue(user.RefreshToken, DateTime.UtcNow.AddMinutes(-5));
            }

            _mockUserRepository
                .Setup(r => r.GetByRefreshToken(tokenString))
                .ReturnsAsync(user);

            var command = new RefreshTokenCommand { RefreshToken = tokenString };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("Refresh token has expired.");
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldReturnNewTokenAndNewRefreshToken()
        {
            // Arrange
            var tokenString = "valid_token";
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

            // Tạo refresh token hợp lệ
            user.IssueRefreshToken(tokenString, TimeSpan.FromMinutes(10));

            _mockUserRepository
                .Setup(r => r.GetByRefreshToken(tokenString))
                .ReturnsAsync(user);

            var command = new RefreshTokenCommand { RefreshToken = tokenString };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Token.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBeNullOrEmpty();
            result.RefreshToken.Should().NotBe(tokenString); // Phải tạo ra một token mới khác token cũ

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockUserRepository.Verify(r => r.UpdateAsync(user.UserID, user, It.IsAny<CancellationToken>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
        }
    }
}
