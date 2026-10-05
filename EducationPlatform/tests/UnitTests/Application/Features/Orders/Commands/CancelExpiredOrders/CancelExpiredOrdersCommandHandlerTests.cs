using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Features.Orders.Commands.CancelExpiredOrders;
using Domain.Common.Interfaces;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Domain.OrderManagement.ValueObject;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Orders.Commands.CancelExpiredOrders
{
    public class CancelExpiredOrdersCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IOrderRepository> _orderRepository = new();
        private readonly CancelExpiredOrdersCommandHandler _handler;

        public CancelExpiredOrdersCommandHandlerTests()
        {
            _unitOfWork.Setup(u => u.GetRepository<IOrderRepository>()).Returns(_orderRepository.Object);
            _handler = new CancelExpiredOrdersCommandHandler(_unitOfWork.Object);
        }

        private static Order NewOrder(IEnumerable<Guid>? couponIds = null) =>
            new(Guid.NewGuid(), Commission.Create(0.15m, 100m), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(-2), couponIds);

        [Fact]
        public async Task NothingExpired_ShouldDoNothing()
        {
            _orderRepository
                .Setup(r => r.GetUnpaidOrdersCreatedBefore(It.IsAny<DateTime>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Order>());

            var cancelled = await _handler.Handle(new CancelExpiredOrdersCommand(), CancellationToken.None);

            cancelled.Should().Be(0);
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task ExpiredOrders_ShouldBeCancelledAndTheirCouponsReleased()
        {
            var coupon = new Coupon(Guid.NewGuid(), Guid.NewGuid(), "COMP", 10m, "Compensation");
            coupon.MarkAsUsed();
            var withCoupon = NewOrder(new[] { coupon.CouponID });
            var withoutCoupon = NewOrder();

            _orderRepository
                .Setup(r => r.GetUnpaidOrdersCreatedBefore(It.IsAny<DateTime>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Order> { withCoupon, withoutCoupon });
            _orderRepository
                .Setup(r => r.GetCouponsByIds(It.Is<IEnumerable<Guid>>(ids => System.Linq.Enumerable.Contains(ids, coupon.CouponID))))
                .ReturnsAsync(new List<Coupon> { coupon });
            _orderRepository
                .Setup(r => r.GetCouponsByIds(It.Is<IEnumerable<Guid>>(ids => !System.Linq.Enumerable.Any(ids))))
                .ReturnsAsync(new List<Coupon>());

            var cancelled = await _handler.Handle(new CancelExpiredOrdersCommand(), CancellationToken.None);

            cancelled.Should().Be(2);
            withCoupon.Status.Should().Be(OrderStatus.Cancelled);
            withoutCoupon.Status.Should().Be(OrderStatus.Cancelled);
            coupon.IsUsed.Should().BeFalse();
            coupon.CurrentUsage.Should().Be(0);
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task Cutoff_ShouldLeaveRoomForLatePayments()
        {
            DateTime? cutoff = null;
            _orderRepository
                .Setup(r => r.GetUnpaidOrdersCreatedBefore(It.IsAny<DateTime>(), It.IsAny<int>()))
                .Callback<DateTime, int, CancellationToken>((c, _, _) => cutoff = c)
                .ReturnsAsync(new List<Order>());

            await _handler.Handle(new CancelExpiredOrdersCommand(), CancellationToken.None);

            var expected = DateTime.UtcNow - Order.PaymentWindow - CancelExpiredOrdersCommandHandler.Grace;
            cutoff.Should().BeCloseTo(expected, TimeSpan.FromSeconds(5));
        }
    }
}
