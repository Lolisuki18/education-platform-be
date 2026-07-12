using System;
using System.Threading;
using System.Threading.Tasks;
using Application.BusinessException;
using Application.Features.Users.Commands;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Users.Commands
{
    public class UpdateUserStatusCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly Mock<Microsoft.Extensions.Caching.Memory.IMemoryCache> _mockMemoryCache;
        private readonly UpdateUserStatusCommandHandler _handler;

        public UpdateUserStatusCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockCurrentUser = new Mock<ICurrentUser>();
            _mockMemoryCache = new Mock<Microsoft.Extensions.Caching.Memory.IMemoryCache>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IUserRepository>())
                .Returns(_mockUserRepository.Object);

            _handler = new UpdateUserStatusCommandHandler(
                _mockUnitOfWork.Object,
                _mockCurrentUser.Object,
                _mockMemoryCache.Object);
        }

        [Fact]
        public async Task Handle_UserSelfDeactivation_ShouldThrowBadRequest()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var command = new UpdateUserStatusCommand { UserId = currentUserId, IsActive = false };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<BadRequest>()
                .WithMessage("Cannot deactivate your own account.");
        }

        [Fact]
        public async Task Handle_UserNotFound_ShouldThrowNotFound()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            _mockUserRepository
                .Setup(r => r.GetByIdAsync(targetUserId))
                .ReturnsAsync((User?)null);

            var command = new UpdateUserStatusCommand { UserId = targetUserId, IsActive = false };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFound>()
                .WithMessage("User not found.");
        }

        [Fact]
        public async Task Handle_ValidDeactivation_ShouldDeactivateAndCommit()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var user = new User(targetUserId, "target@example.com", "Password123!", "0900000001", "Target User", null, Role.Student, DateTime.UtcNow, true);
            user.IsActive.Should().BeTrue();

            _mockUserRepository
                .Setup(r => r.GetByIdAsync(targetUserId))
                .ReturnsAsync(user);

            var command = new UpdateUserStatusCommand { UserId = targetUserId, IsActive = false };

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            user.IsActive.Should().BeFalse();
            _mockUnitOfWork.Verify(u => u.CommitAsync(currentUserId.ToString()), Times.Once);
        }
    }
}
