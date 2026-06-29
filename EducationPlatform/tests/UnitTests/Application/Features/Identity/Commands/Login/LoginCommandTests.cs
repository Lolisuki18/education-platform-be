using Application.BusinessException;
using Application.Features.Identity.Commands.Login;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
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
        private readonly LoginCommandHandler _handler;

        public LoginCommandTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IUserRepository>())
                .Returns(_mockUserRepository.Object);

            var inMemorySettings = new Dictionary<string, string?> {
                {"JwtSettings:SecretKey", "THIS_IS_A_TEST_SECRET_KEY_AT_LEAST_32_CHARS"},
                {"JwtSettings:Issuer", "EducationPlatform"},
                {"JwtSettings:Audience", "EducationPlatform"},
                {"JwtSettings:ExpiryMinutes", "60"}
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _handler = new LoginCommandHandler(_mockUnitOfWork.Object, configuration);
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
            _mockUserRepository.Verify(r => r.Update(user.UserID, user), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotFound_ShouldThrowNotFoundException()
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
            await act.Should().ThrowAsync<NotFound>()
                .WithMessage($"User with email: {email} not found.");
        }

        [Fact]
        public async Task Handle_InvalidPassword_ShouldThrowAuthenticateException()
        {
            // Arrange
            var email = "test@gmail.com";
            var user = new User(
                Guid.NewGuid(),
                email,
                "correctpassword",
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
                .WithMessage("Invalid password or email has not been verified.");
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
                .WithMessage("Invalid password or email has not been verified.");
        }
    }
}