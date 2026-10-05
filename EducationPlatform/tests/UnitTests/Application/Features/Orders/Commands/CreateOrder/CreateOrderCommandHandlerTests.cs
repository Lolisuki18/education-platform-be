using Application.Exceptions;
using Application.Features.Orders.Commands.CreateOrder;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Domain.OrderManagement.Enum;
using Domain.OrderManagement.ValueObject;

namespace UnitTests.Application.Features.Orders.Commands.CreateOrder
{
    public class CreateOrderCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly Mock<IPaymentService> _mockPaymentService;
        private readonly CreateOrderCommandHandler _handler;

        public CreateOrderCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();
            _mockPaymentService = new Mock<IPaymentService>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<ICourseRepository>())
                .Returns(_mockCourseRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IOrderRepository>())
                .Returns(_mockOrderRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _mockEnrollmentRepository
                .Setup(r => r.IsStudentEnrolled(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _mockOrderRepository
                .Setup(r => r.GetCouponsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Coupon>());

            // Mirror what AutoMapper does for the real Order -> OrderDTO map
            _mockMapper
                .Setup(m => m.Map<OrderDTO>(It.IsAny<Order>()))
                .Returns((Order o) => new OrderDTO
                {
                    OrderID = o.OrderID,
                    Status = o.Status,
                    CheckoutUrl = o.CheckoutUrl
                });

            _handler = new CreateOrderCommandHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object,
                _mockPaymentService.Object,
                NullLogger<CreateOrderCommandHandler>.Instance);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var command = new CreateOrderCommand { CourseID = Guid.NewGuid() };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("User must be authenticated to create an order.");
        }

        [Fact]
        public async Task Handle_CourseNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            _mockCourseRepository
                .Setup(r => r.GetByIdAsync(courseId))
                .ReturnsAsync((Course?)null);

            var command = new CreateOrderCommand { CourseID = courseId };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage($"Course with ID: {courseId} not found.");
        }

        [Fact]
        public async Task Handle_AlreadyEnrolled_ShouldThrowConflictException()
        {
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);
            _mockCourseRepository.Setup(r => r.GetByIdAsync(courseId)).ReturnsAsync(CreateCourseInstance(courseId, 100m));
            _mockEnrollmentRepository
                .Setup(r => r.IsStudentEnrolled(studentId, courseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            Func<Task> act = async () => await _handler.Handle(new CreateOrderCommand { CourseID = courseId }, CancellationToken.None);

            await act.Should().ThrowAsync<ConflictException>().WithMessage("Student is already enrolled in this course.");
            _mockOrderRepository.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ValidRequestWithoutCoupons_ShouldCreateOrderAndReturnDTO()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            var course = CreateCourseInstance(courseId, 100m);

            _mockCourseRepository
                .Setup(r => r.GetByIdAsync(courseId))
                .ReturnsAsync(course);

            _mockPaymentService
                .Setup(p => p.CreatePaymentLinkAsync(It.IsAny<long>(), 100m, It.IsAny<string>()))
                .ReturnsAsync("https://checkout.payos.vn/payment-link");

            var command = new CreateOrderCommand
            {
                CourseID = courseId,
                CouponIds = null
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.CheckoutUrl.Should().Be("https://checkout.payos.vn/payment-link");
            result.Status.Should().Be(OrderStatus.Created);

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockOrderRepository.Verify(r => r.Add(It.Is<Order>(o =>
                o.StudentID == studentId &&
                o.CourseID == courseId &&
                o.PlatformAmount == 15m && // 100 * 15% = 15
                o.TeacherAmount == 85m
            )), Times.Once);
            // One commit for the order, one for the checkout link
            _mockUnitOfWork.Verify(u => u.CommitAsync(null), Times.Exactly(2));
        }

        [Fact]
        public async Task Handle_ValidRequestWithCoupons_ShouldApplyDiscountCreateOrderAndMarkCouponsAsUsed()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var otherStudentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            var course = CreateCourseInstance(courseId, 100m);

            _mockCourseRepository
                .Setup(r => r.GetByIdAsync(courseId))
                .ReturnsAsync(course);

            var validCoupon = new Coupon(Guid.NewGuid(), studentId, "COUPON1", 10m, "Reason 1");
            var usedCoupon = new Coupon(Guid.NewGuid(), studentId, "COUPON2", 15m, "Reason 2");
            usedCoupon.MarkAsUsed();
            var otherUserCoupon = new Coupon(Guid.NewGuid(), otherStudentId, "COUPON3", 20m, "Reason 3");

            _mockOrderRepository
                .Setup(r => r.GetCouponsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Coupon> { validCoupon, usedCoupon, otherUserCoupon });

            // Price after discount: 100 - 10 = 90
            _mockPaymentService
                .Setup(p => p.CreatePaymentLinkAsync(It.IsAny<long>(), 90m, It.IsAny<string>()))
                .ReturnsAsync("https://checkout.payos.vn/payment-link-discount");

            var command = new CreateOrderCommand
            {
                CourseID = courseId,
                CouponIds = new List<Guid> { validCoupon.CouponID, usedCoupon.CouponID, otherUserCoupon.CouponID }
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.CheckoutUrl.Should().Be("https://checkout.payos.vn/payment-link-discount");

            // Only the valid coupon is consumed
            validCoupon.IsUsed.Should().BeTrue();
            otherUserCoupon.IsUsed.Should().BeFalse();

            _mockOrderRepository.Verify(r => r.Add(It.Is<Order>(o =>
                o.StudentID == studentId &&
                o.CourseID == courseId &&
                o.PlatformAmount == 13.5m && // 90 * 15% = 13.5
                o.TeacherAmount == 76.5m &&
                o.CouponIds.Count == 1 &&
                o.CouponIds[0] == validCoupon.CouponID
            )), Times.Once);
        }

        [Fact]
        public async Task Handle_PaymentLinkFails_ShouldCancelOrderAndReleaseCoupons()
        {
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);
            _mockCourseRepository.Setup(r => r.GetByIdAsync(courseId)).ReturnsAsync(CreateCourseInstance(courseId, 100m));

            var coupon = new Coupon(Guid.NewGuid(), studentId, "COUPON1", 10m, "Reason");
            _mockOrderRepository
                .Setup(r => r.GetCouponsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Coupon> { coupon });

            Order? savedOrder = null;
            _mockOrderRepository.Setup(r => r.Add(It.IsAny<Order>())).Callback<Order>(o => savedOrder = o);

            _mockPaymentService
                .Setup(p => p.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("PayOS is down"));

            Func<Task> act = async () => await _handler.Handle(
                new CreateOrderCommand { CourseID = courseId, CouponIds = new List<Guid> { coupon.CouponID } },
                CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("PayOS is down");

            savedOrder.Should().NotBeNull();
            savedOrder!.Status.Should().Be(OrderStatus.Cancelled);
            coupon.CurrentUsage.Should().Be(0);
            coupon.IsUsed.Should().BeFalse();
        }

        [Fact]
        public async Task Handle_FullyDiscounted_ShouldEnrollWithoutCallingPaymentGateway()
        {
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);
            _mockCourseRepository.Setup(r => r.GetByIdAsync(courseId)).ReturnsAsync(CreateCourseInstance(courseId, 100m));

            var coupon = new Coupon(Guid.NewGuid(), studentId, "FULL", 150m, "Covers everything");
            _mockOrderRepository
                .Setup(r => r.GetCouponsByIds(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<Coupon> { coupon });

            var result = await _handler.Handle(
                new CreateOrderCommand { CourseID = courseId, CouponIds = new List<Guid> { coupon.CouponID } },
                CancellationToken.None);

            result.Status.Should().Be(OrderStatus.Pending);
            _mockPaymentService.Verify(
                p => p.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>()),
                Times.Never);
            _mockOrderRepository.Verify(r => r.Add(It.Is<Order>(o => o.IsPaid && o.PlatformAmount == 0m)), Times.Once);
        }

        [Fact]
        public async Task Handle_AwaitingOrderStillValid_ShouldReturnExistingCheckoutLink()
        {
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);
            _mockCourseRepository.Setup(r => r.GetByIdAsync(courseId)).ReturnsAsync(CreateCourseInstance(courseId, 100m));

            var existing = new Order(Guid.NewGuid(), Commission.Create(0.15m, 100m), studentId, courseId, null);
            existing.AttachCheckoutUrl("https://checkout.payos.vn/existing");
            _mockOrderRepository.Setup(r => r.GetAwaitingPaymentOrder(studentId, courseId)).ReturnsAsync(existing);

            var result = await _handler.Handle(new CreateOrderCommand { CourseID = courseId }, CancellationToken.None);

            result.CheckoutUrl.Should().Be("https://checkout.payos.vn/existing");
            _mockOrderRepository.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
            _mockPaymentService.Verify(
                p => p.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Handle_AwaitingOrderExpired_ShouldCancelItAndCreateANewOne()
        {
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);
            _mockCourseRepository.Setup(r => r.GetByIdAsync(courseId)).ReturnsAsync(CreateCourseInstance(courseId, 100m));

            var stale = new Order(
                Guid.NewGuid(),
                Commission.Create(0.15m, 100m),
                studentId,
                courseId,
                DateTime.UtcNow.AddHours(-1));
            stale.AttachCheckoutUrl("https://checkout.payos.vn/stale");
            _mockOrderRepository.Setup(r => r.GetAwaitingPaymentOrder(studentId, courseId)).ReturnsAsync(stale);

            _mockPaymentService
                .Setup(p => p.CreatePaymentLinkAsync(It.IsAny<long>(), 100m, It.IsAny<string>()))
                .ReturnsAsync("https://checkout.payos.vn/fresh");

            var result = await _handler.Handle(new CreateOrderCommand { CourseID = courseId }, CancellationToken.None);

            stale.Status.Should().Be(OrderStatus.Cancelled);
            result.CheckoutUrl.Should().Be("https://checkout.payos.vn/fresh");
            _mockOrderRepository.Verify(r => r.Add(It.IsAny<Order>()), Times.Once);
        }

        private Course CreateCourseInstance(Guid courseId, decimal priceAmount)
        {
            return new Course(
                courseId,
                "Purchase Course",
                "Description",
                priceAmount,
                "thumbnail.png",
                "purchase-course",
                "Prerequisites",
                "Outcomes",
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTime.UtcNow
            );
        }
    }
}
