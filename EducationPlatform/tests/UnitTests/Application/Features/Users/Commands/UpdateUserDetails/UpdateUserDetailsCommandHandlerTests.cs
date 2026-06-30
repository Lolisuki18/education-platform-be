using Application.BusinessException;
using Application.Features.Users.Commands;
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

namespace UnitTests.Application.Features.Users.Commands
{
    public class UpdateUserDetailsCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly UpdateUserDetailsCommandHandler _handler;

        public UpdateUserDetailsCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IUserRepository>())
                .Returns(_mockUserRepository.Object);

            _handler = new UpdateUserDetailsCommandHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var command = new UpdateUserDetailsCommand();

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

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

            var command = new UpdateUserDetailsCommand
            {
                UserId = targetUserId
            };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFound>()
                .WithMessage($"User with ID: {targetUserId} not found.");
        }

        [Fact]
        public async Task Handle_EmptyUserIdInRequest_ShouldFallbackToCurrentUserIdAndUpdateSuccessfully()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var user = new User(
                currentUserId,
                "test@gmail.com",
                "password123",
                "0123456789",
                "Old Name",
                "Old Bio",
                Role.Student,
                DateTime.UtcNow,
                isVerified: true
            );

            _mockUserRepository
                .Setup(r => r.GetByIdAsync(currentUserId))
                .ReturnsAsync(user);

            var expectedDto = new UserDTO { UserID = currentUserId, Name = "New Name" };
            _mockMapper
                .Setup(m => m.Map<UserDTO>(user))
                .Returns(expectedDto);

            var command = new UpdateUserDetailsCommand
            {
                UserId = Guid.Empty, // rỗng -> lấy currentUserId
                Name = "New Name",
                Phone = "0987654321",
                Bio = "New Bio"
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.UserID.Should().Be(currentUserId);
            result.Name.Should().Be("New Name");

            // Xác thực các thuộc tính Domain của User được cập nhật chính xác
            user.Name.Should().Be("New Name");
            user.Phone.Should().Be("0987654321");
            user.Bio.Should().Be("New Bio");

            _mockUnitOfWork.Verify(u => u.CommitAsync(currentUserId.ToString()), Times.Once);
        }

        [Fact]
        public async Task Handle_SpecificUserIdInRequest_ShouldUpdateSpecifiedUserSuccessfully()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(adminId);

            var targetUser = new User(
                targetUserId,
                "target@gmail.com",
                "password123",
                "0123456789",
                "Old Target Name",
                "Old Target Bio",
                Role.Student,
                DateTime.UtcNow,
                isVerified: true
            );

            _mockUserRepository
                .Setup(r => r.GetByIdAsync(targetUserId))
                .ReturnsAsync(targetUser);

            var expectedDto = new UserDTO { UserID = targetUserId, Name = "New Target Name" };
            _mockMapper
                .Setup(m => m.Map<UserDTO>(targetUser))
                .Returns(expectedDto);

            var command = new UpdateUserDetailsCommand
            {
                UserId = targetUserId, // cập nhật cho user khác
                Name = "New Target Name",
                Phone = "0987654321",
                Bio = "New Target Bio"
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.UserID.Should().Be(targetUserId);

            targetUser.Name.Should().Be("New Target Name");
            targetUser.Phone.Should().Be("0987654321");
            targetUser.Bio.Should().Be("New Target Bio");

            _mockUnitOfWork.Verify(u => u.CommitAsync(adminId.ToString()), Times.Once);
        }
    }
}
