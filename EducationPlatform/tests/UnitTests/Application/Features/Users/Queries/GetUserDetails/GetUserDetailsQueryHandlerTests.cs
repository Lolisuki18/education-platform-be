using Application.BusinessException;
using Application.Features.Users.Queries;
using Application.Interface;
using Application.Results;
using AutoMapper;
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

namespace UnitTests.Application.Features.Users.Queries
{
    public class GetUserDetailsQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly GetUserDetailsHandler _handler;

        public GetUserDetailsQueryHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IUserRepository>())
                .Returns(_mockUserRepository.Object);

            _handler = new GetUserDetailsHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var query = new GetUserDetailsQuery();

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("User must be authenticated.");
        }

        [Fact]
        public async Task Handle_UserDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            _mockUserRepository
                .Setup(r => r.GetByIdAsync(targetUserId))
                .ReturnsAsync((User?)null);

            var query = new GetUserDetailsQuery { UserId = targetUserId };

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFound>()
                .WithMessage("User detail not found");
        }

        [Fact]
        public async Task Handle_EmptyUserIdInRequest_ShouldFallbackToCurrentUserIdAndReturnDetails()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var user = new User(
                currentUserId,
                "test@gmail.com",
                "password123",
                "0123456789",
                "Test User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: true
            );

            _mockUserRepository
                .Setup(r => r.GetByIdAsync(currentUserId))
                .ReturnsAsync(user);

            var expectedDto = new UserDTO { UserID = currentUserId, Name = "Test User" };
            _mockMapper
                .Setup(m => m.Map<UserDTO>(user))
                .Returns(expectedDto);

            var query = new GetUserDetailsQuery { UserId = Guid.Empty };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.UserID.Should().Be(currentUserId);
            result.Name.Should().Be("Test User");
            _mockUserRepository.Verify(r => r.GetByIdAsync(currentUserId), Times.Once);
        }

        [Fact]
        public async Task Handle_SpecificUserIdInRequest_ShouldReturnTargetUserDetails()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var targetUser = new User(
                targetUserId,
                "target@gmail.com",
                "password123",
                "0123456789",
                "Target User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: true
            );

            _mockUserRepository
                .Setup(r => r.GetByIdAsync(targetUserId))
                .ReturnsAsync(targetUser);

            var expectedDto = new UserDTO { UserID = targetUserId, Name = "Target User" };
            _mockMapper
                .Setup(m => m.Map<UserDTO>(targetUser))
                .Returns(expectedDto);

            var query = new GetUserDetailsQuery { UserId = targetUserId };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.UserID.Should().Be(targetUserId);
            result.Name.Should().Be("Target User");
            _mockUserRepository.Verify(r => r.GetByIdAsync(targetUserId), Times.Once);
        }
    }
}
