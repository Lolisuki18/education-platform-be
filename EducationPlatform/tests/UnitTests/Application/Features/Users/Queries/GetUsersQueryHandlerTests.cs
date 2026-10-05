using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Features.Users.Queries;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Users.Queries
{
    public class GetUsersQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly GetUsersQueryHandler _handler;

        public GetUsersQueryHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IUserRepository>())
                .Returns(_mockUserRepository.Object);

            _handler = new GetUsersQueryHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var query = new GetUsersQuery();

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("User must be authenticated.");
        }

        [Fact]
        public async Task Handle_WithValidRequest_ShouldReturnPagedUsers()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns(Guid.NewGuid());

            var users = new List<User>
            {
                new User(Guid.NewGuid(), "test1@example.com", "Password123!", "0900000001", "User One", null, Role.Student, DateTime.UtcNow, true)
            };

            _mockUserRepository
                .Setup(r => r.GetUsersPaged(1, 10, null))
                .ReturnsAsync((users, 1));

            var userDtos = new List<UserDTO>
            {
                new UserDTO { UserID = users[0].UserID, Email = "test1@example.com", Name = "User One" }
            };

            _mockMapper
                .Setup(m => m.Map<IEnumerable<UserDTO>>(users))
                .Returns(userDtos);

            var query = new GetUsersQuery { PageIndex = 1, PageSize = 10, Role = null };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.TotalItems.Should().Be(1);
            result.PageIndex.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.HasNextPage.Should().BeFalse();
            result.HasPreviousPage.Should().BeFalse();
        }
    }
}
