using Domain.DomainExceptions;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Domain.OrderManagement.Events;
using Domain.OrderManagement.ValueObject;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace UnitTests.DomainTests.OrderManagement
{
    public class OrderTests
    {
        // ======================= COUPON TESTS =======================
        [Fact]
        public void CouponConstructor_EmptyCouponId_ShouldThrowDomainException()
        {
            Action act = () => new Coupon(Guid.Empty, Guid.NewGuid(), "CODE", 10m, "Reason");
            act.Should().Throw<DomainException>().WithMessage("Coupon ID cannot be empty");
        }

        [Fact]
        public void CouponConstructor_EmptyStudentId_ShouldThrowDomainException()
        {
            Action act = () => new Coupon(Guid.NewGuid(), Guid.Empty, "CODE", 10m, "Reason");
            act.Should().Throw<DomainException>().WithMessage("Student ID cannot be empty");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CouponConstructor_InvalidCode_ShouldThrowDomainException(string? code)
        {
            Action act = () => new Coupon(Guid.NewGuid(), Guid.NewGuid(), code!, 10m, "Reason");
            act.Should().Throw<DomainException>();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void CouponConstructor_InvalidDiscount_ShouldThrowDomainException(decimal discount)
        {
            Action act = () => new Coupon(Guid.NewGuid(), Guid.NewGuid(), "CODE", discount, "Reason");
            act.Should().Throw<DomainException>().WithMessage("Discount amount must be greater than zero");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CouponConstructor_InvalidReason_ShouldThrowDomainException(string? reason)
        {
            Action act = () => new Coupon(Guid.NewGuid(), Guid.NewGuid(), "CODE", 10m, reason!);
            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void CouponConstructor_ValidArguments_ShouldCreateSuccessfully()
        {
            var couponId = Guid.NewGuid();
            var studentId = Guid.NewGuid();

            var coupon = new Coupon(couponId, studentId, "  DISCOUNT10  ", 10m, "  Promotion  ");

            coupon.CouponID.Should().Be(couponId);
            coupon.StudentID.Should().Be(studentId);
            coupon.Code.Should().Be("DISCOUNT10");
            coupon.DiscountAmount.Should().Be(10m);
            coupon.Reason.Should().Be("Promotion");
            coupon.IsUsed.Should().BeFalse();
            coupon.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Coupon_MarkAsUsed_ShouldWorkAndThrowIfAlreadyUsed()
        {
            var coupon = new Coupon(Guid.NewGuid(), Guid.NewGuid(), "CODE", 10m, "Reason");

            coupon.MarkAsUsed();
            coupon.IsUsed.Should().BeTrue();

            Action act = () => coupon.MarkAsUsed();
            act.Should().Throw<DomainException>().WithMessage("Coupon already used");
        }

        // ======================= COMMISSION TESTS =======================
        [Theory]
        [InlineData(-0.1)]
        [InlineData(1.1)]
        public void CommissionCreate_InvalidRate_ShouldThrowDomainException(decimal rate)
        {
            Action act = () => Commission.Create(rate, 100m);
            act.Should().Throw<DomainException>().WithMessage("Invalid commission rate");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public void CommissionCreate_InvalidTotal_ShouldThrowDomainException(decimal total)
        {
            Action act = () => Commission.Create(0.15m, total);
            act.Should().Throw<DomainException>().WithMessage("Total amount must be greater than zero");
        }

        [Fact]
        public void CommissionCreate_ValidArguments_ShouldCalculateAmountsCorrectly()
        {
            var commission = Commission.Create(0.15m, 200m);

            commission.PlatformRate.Should().Be(0.15m);
            commission.PlatformAmount.Should().Be(30m); // 200 * 15%
            commission.TeacherAmount.Should().Be(170m); // 200 - 30
        }

        // ======================= ORDER TESTS =======================
        [Fact]
        public void OrderConstructor_EmptyOrderId_ShouldThrowDomainException()
        {
            var commission = Commission.Create(0.15m, 100m);
            Action act = () => new Order(Guid.Empty, commission, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            act.Should().Throw<DomainException>().WithMessage("Order ID cannot be empty");
        }

        [Fact]
        public void OrderConstructor_EmptyStudentId_ShouldThrowDomainException()
        {
            var commission = Commission.Create(0.15m, 100m);
            Action act = () => new Order(Guid.NewGuid(), commission, Guid.Empty, Guid.NewGuid(), DateTime.UtcNow);
            act.Should().Throw<DomainException>().WithMessage("Student ID cannot be empty");
        }

        [Fact]
        public void OrderConstructor_EmptyCourseId_ShouldThrowDomainException()
        {
            var commission = Commission.Create(0.15m, 100m);
            Action act = () => new Order(Guid.NewGuid(), commission, Guid.NewGuid(), Guid.Empty, DateTime.UtcNow);
            act.Should().Throw<DomainException>().WithMessage("Course ID cannot be empty");
        }

        [Fact]
        public void OrderConstructor_ValidArguments_ShouldCreateOrderSuccessfully()
        {
            var orderId = Guid.NewGuid();
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var commission = Commission.Create(0.15m, 100m);
            var createdAt = DateTime.UtcNow;

            var order = new Order(orderId, commission, studentId, courseId, createdAt);

            order.OrderID.Should().Be(orderId);
            order.OrderCode.Should().BeCloseTo(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 1000);
            order.PlatformAmount.Should().Be(15m);
            order.TeacherAmount.Should().Be(85m);
            order.Method.Should().Be(OrderMethod.PayOS);
            order.Status.Should().Be(OrderStatus.Created);
            order.CreatedAt.Should().Be(createdAt);
            order.StudentID.Should().Be(studentId);
            order.CourseID.Should().Be(courseId);
            order.PaidAt.Should().BeNull();
        }

        [Fact]
        public void Order_StudentPaid_ShouldUpdateStatusAndRaiseEvent()
        {
            var commission = Commission.Create(0.15m, 100m);
            var order = new Order(Guid.NewGuid(), commission, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            var paidAt = DateTime.UtcNow;

            order.StudentPaid(paidAt);

            order.Status.Should().Be(OrderStatus.Pending);
            order.PaidAt.Should().Be(paidAt);

            // Verify Domain Event was added
            order.DomainEvents.Should().HaveCount(1);
            var domainEvent = order.DomainEvents.First();
            domainEvent.Should().BeOfType<OrderPaidEvent>();

            var orderPaidEvent = (OrderPaidEvent)domainEvent;
            orderPaidEvent.OrderID.Should().Be(order.OrderID);
            orderPaidEvent.StudentID.Should().Be(order.StudentID);
            orderPaidEvent.CourseID.Should().Be(order.CourseID);
            orderPaidEvent.PaidAt.Should().Be(paidAt);
        }
    }
}
