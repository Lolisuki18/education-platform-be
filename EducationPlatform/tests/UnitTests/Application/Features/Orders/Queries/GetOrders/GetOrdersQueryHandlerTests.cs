using Application.Exceptions;
using Application.Features.Orders.Queries.GetOrders;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Domain.OrderManagement.ValueObject;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Orders.Queries.GetOrders
{
    public class GetOrdersQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly GetOrdersQueryHandler _handler;

        public GetOrdersQueryHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IOrderRepository>())
                .Returns(_mockOrderRepository.Object);

            _handler = new GetOrdersQueryHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            _mockCurrentUser.Setup(u => u.Role).Returns((string?)null);
            var query = new GetOrdersQuery();

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("User must be authenticated.");
        }

        [Fact]
        public async Task Handle_InvalidRole_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns(Guid.NewGuid());
            _mockCurrentUser.Setup(u => u.Role).Returns("InvalidRole");
            var query = new GetOrdersQuery();

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("Invalid role");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Handle_NoOrders_ShouldReturnAnEmptyList(bool repositoryReturnsNull)
        {
            // Arrange
            var adminId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(adminId);
            _mockCurrentUser.Setup(u => u.Role).Returns("Admin");

            _mockOrderRepository
                .Setup(r => r.GetOrders(null, 1, 10, null, null))
                .ReturnsAsync(repositoryReturnsNull ? (IEnumerable<Order>)null! : new List<Order>());

            var query = new GetOrdersQuery { PageIndex = 1, PageSize = 10 };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_StudentRole_ShouldScopeByStudentId()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);
            _mockCurrentUser.Setup(u => u.Role).Returns("Student");

            var commission = Commission.Create(0.15m, 100m);
            var orders = new List<Order>
            {
                new Order(Guid.NewGuid(), commission, studentId, Guid.NewGuid(), DateTime.UtcNow)
            };

            _mockOrderRepository
                .Setup(r => r.GetOrders("Created", 1, 10, null, studentId))
                .ReturnsAsync(orders);

            var expectedDtos = new List<OrderDTO>
            {
                new OrderDTO { OrderID = orders[0].OrderID, StudentID = studentId }
            };

            _mockMapper
                .Setup(m => m.Map<IEnumerable<OrderDTO>>(orders))
                .Returns(expectedDtos);

            var query = new GetOrdersQuery
            {
                OrderStatus = OrderStatus.Created,
                PageIndex = 1,
                PageSize = 10
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeEquivalentTo(expectedDtos);
            _mockOrderRepository.Verify(r => r.GetOrders("Created", 1, 10, null, studentId), Times.Once);
        }

        [Fact]
        public async Task Handle_TeacherRole_ShouldScopeByTeacherId()
        {
            // Arrange
            var teacherId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(teacherId);
            _mockCurrentUser.Setup(u => u.Role).Returns("Teacher");

            var commission = Commission.Create(0.15m, 100m);
            var orders = new List<Order>
            {
                new Order(Guid.NewGuid(), commission, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)
            };

            _mockOrderRepository
                .Setup(r => r.GetOrders(null, 1, 10, teacherId, null))
                .ReturnsAsync(orders);

            var expectedDtos = new List<OrderDTO>
            {
                new OrderDTO { OrderID = orders[0].OrderID, TeacherAmount = 85m }
            };

            _mockMapper
                .Setup(m => m.Map<IEnumerable<OrderDTO>>(orders))
                .Returns(expectedDtos);

            var query = new GetOrdersQuery
            {
                PageIndex = 1,
                PageSize = 10
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeEquivalentTo(expectedDtos);
            _mockOrderRepository.Verify(r => r.GetOrders(null, 1, 10, teacherId, null), Times.Once);
        }

        [Fact]
        public async Task Handle_AdminRole_ShouldNotScopeAnyId()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(adminId);
            _mockCurrentUser.Setup(u => u.Role).Returns("Admin");

            var commission = Commission.Create(0.15m, 100m);
            var orders = new List<Order>
            {
                new Order(Guid.NewGuid(), commission, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow),
                new Order(Guid.NewGuid(), commission, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow)
            };

            _mockOrderRepository
                .Setup(r => r.GetOrders(null, 1, 10, null, null))
                .ReturnsAsync(orders);

            var expectedDtos = new List<OrderDTO>
            {
                new OrderDTO { OrderID = orders[0].OrderID },
                new OrderDTO { OrderID = orders[1].OrderID }
            };

            _mockMapper
                .Setup(m => m.Map<IEnumerable<OrderDTO>>(orders))
                .Returns(expectedDtos);

            var query = new GetOrdersQuery
            {
                PageIndex = 1,
                PageSize = 10
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeEquivalentTo(expectedDtos);
            _mockOrderRepository.Verify(r => r.GetOrders(null, 1, 10, null, null), Times.Once);
        }
    }
}
