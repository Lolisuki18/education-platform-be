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
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            _mockUserRepository
                .Setup(r => r.GetByIdAsync(currentUserId))
                .ReturnsAsync((User?)null);

            var command = new UpdateUserDetailsCommand();

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFound>()
                .WithMessage($"User with ID: {currentUserId} not found.");
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldUpdateCurrentUserOnlySuccessfully()
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

            user.Name.Should().Be("New Name");
            user.Phone.Should().Be("0987654321");
            user.Bio.Should().Be("New Bio");

            _mockUnitOfWork.Verify(u => u.CommitAsync(currentUserId.ToString()), Times.Once);
            // A caller can never target another user's account: only GetByIdAsync(currentUserId) is ever invoked.
            _mockUserRepository.Verify(r => r.GetByIdAsync(It.Is<Guid>(id => id != currentUserId)), Times.Never);
        }
    }
}
