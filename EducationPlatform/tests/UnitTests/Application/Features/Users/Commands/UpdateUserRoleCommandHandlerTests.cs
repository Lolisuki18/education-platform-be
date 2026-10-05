using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
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
    public class UpdateUserRoleCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly UpdateUserRoleCommandHandler _handler;

        public UpdateUserRoleCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IUserRepository>())
                .Returns(_mockUserRepository.Object);

            _handler = new UpdateUserRoleCommandHandler(
                _mockUnitOfWork.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_UserSelfRoleChange_ShouldThrowBadRequest()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var command = new UpdateUserRoleCommand { UserId = currentUserId, Role = Role.Admin };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<BadRequestException>()
                .WithMessage("Cannot change your own role.");
        }

        [Fact]
        public async Task Handle_ValidRoleChange_ShouldUpdateRoleAndCommit()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var user = new User(targetUserId, "target@example.com", "Password123!", "0900000001", "Target User", null, Role.Student, DateTime.UtcNow, true);
            user.Role.Should().Be(Role.Student);

            _mockUserRepository
                .Setup(r => r.GetByIdAsync(targetUserId))
                .ReturnsAsync(user);

            var command = new UpdateUserRoleCommand { UserId = targetUserId, Role = Role.Teacher };

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            user.Role.Should().Be(Role.Teacher);
            _mockUnitOfWork.Verify(u => u.CommitAsync(currentUserId.ToString()), Times.Once);
        }
    }
}
