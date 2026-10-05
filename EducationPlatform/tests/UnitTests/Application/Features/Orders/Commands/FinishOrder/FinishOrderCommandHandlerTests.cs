using Application.Exceptions;
using Application.Features.Orders.Commands.FinishOrder;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Domain.OrderManagement.ValueObject;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests.Application.Features.Orders.Commands.FinishOrder
{
    public class FinishOrderCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly FinishOrderCommandHandler _handler;

        public FinishOrderCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockMapper = new Mock<IMapper>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IOrderRepository>())
                .Returns(_mockOrderRepository.Object);

            _handler = new FinishOrderCommandHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                NullLogger<FinishOrderCommandHandler>.Instance);
        }

        [Fact]
        public async Task Handle_OrderNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var orderCode = 123456789L;
            _mockOrderRepository
                .Setup(r => r.GetOrderByOrderCode(orderCode))
                .ReturnsAsync((Order?)null);

            var command = new FinishOrderCommand { OrderCode = orderCode };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage($"Order with code: {orderCode} not found.");
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldFinishOrderAndCommitSuccessfully()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var commission = Commission.Create(0.15m, 100m);
            var order = new Order(orderId, commission, studentId, courseId, DateTime.UtcNow);
            var orderCode = order.OrderCode;

            _mockOrderRepository
                .Setup(r => r.GetOrderByOrderCode(orderCode))
                .ReturnsAsync(order);

            var expectedDto = new OrderDTO { OrderID = orderId, Status = OrderStatus.Pending };
            _mockMapper
                .Setup(m => m.Map<OrderDTO>(order))
                .Returns(expectedDto);

            var command = new FinishOrderCommand { OrderCode = orderCode };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.OrderID.Should().Be(orderId);

            // Kiểm tra nghiệp vụ: Trạng thái Order cập nhật thành Pending và gán thời gian thanh toán
            order.Status.Should().Be(OrderStatus.Pending);
            order.PaidAt.Should().NotBeNull();

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockOrderRepository.Verify(r => r.UpdateAsync(orderId, order, It.IsAny<CancellationToken>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(studentId.ToString()), Times.Once);
        }

        [Fact]
        public async Task Handle_OrderAlreadyPaid_ShouldBeIdempotent()
        {
            var order = new Order(Guid.NewGuid(), Commission.Create(0.15m, 100m), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            order.StudentPaid(null);
            order.ClearDomainEvents();

            _mockOrderRepository.Setup(r => r.GetOrderByOrderCode(order.OrderCode)).ReturnsAsync(order);
            _mockMapper.Setup(m => m.Map<OrderDTO>(order)).Returns(new OrderDTO { OrderID = order.OrderID });

            await _handler.Handle(new FinishOrderCommand { OrderCode = order.OrderCode }, CancellationToken.None);

            _mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
            order.DomainEvents.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_AmountDoesNotMatch_ShouldThrowAndLeaveOrderUnpaid()
        {
            var order = new Order(Guid.NewGuid(), Commission.Create(0.15m, 100m), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            _mockOrderRepository.Setup(r => r.GetOrderByOrderCode(order.OrderCode)).ReturnsAsync(order);

            Func<Task> act = async () => await _handler.Handle(
                new FinishOrderCommand { OrderCode = order.OrderCode, PaidAmount = 1 }, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>();
            order.Status.Should().Be(OrderStatus.Created);
        }

        [Fact]
        public async Task Handle_CancelledOrderPaidLate_ShouldStillBeActivated()
        {
            var order = new Order(Guid.NewGuid(), Commission.Create(0.15m, 100m), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            order.Cancel();
            _mockOrderRepository.Setup(r => r.GetOrderByOrderCode(order.OrderCode)).ReturnsAsync(order);
            _mockMapper.Setup(m => m.Map<OrderDTO>(order)).Returns(new OrderDTO { OrderID = order.OrderID });

            await _handler.Handle(new FinishOrderCommand { OrderCode = order.OrderCode, PaidAmount = 100 }, CancellationToken.None);

            order.Status.Should().Be(OrderStatus.Pending);
        }
    }
}
