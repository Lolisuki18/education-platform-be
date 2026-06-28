using Application.BusinessException;
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
                _mockMapper.Object);
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
            await act.Should().ThrowAsync<NotFound>()
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
            _mockOrderRepository.Verify(r => r.Update(orderId, order), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(studentId.ToString()), Times.Once);
        }
    }
}
