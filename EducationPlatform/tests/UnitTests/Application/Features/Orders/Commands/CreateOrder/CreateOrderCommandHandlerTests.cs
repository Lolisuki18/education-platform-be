using Application.BusinessException;
using Application.Features.Orders.Commands.CreateOrder;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Orders.Commands.CreateOrder
{
    public class CreateOrderCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly Mock<IPaymentService> _mockPaymentService;
        private readonly CreateOrderCommandHandler _handler;

        public CreateOrderCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();
            _mockPaymentService = new Mock<IPaymentService>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<ICourseRepository>())
                .Returns(_mockCourseRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IOrderRepository>())
                .Returns(_mockOrderRepository.Object);

            _handler = new CreateOrderCommandHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object,
                _mockPaymentService.Object);
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
            await act.Should().ThrowAsync<NotFound>()
                .WithMessage($"Course with ID: {courseId} not found.");
        }

        [Fact]
        public async Task Handle_ValidRequestWithoutCoupons_ShouldCreateOrderAndReturnDTO()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            var course = CreateCourseInstance(courseId, 100m); // Giá khóa học là 100

            _mockCourseRepository
                .Setup(r => r.GetByIdAsync(courseId))
                .ReturnsAsync(course);

            _mockPaymentService
                .Setup(p => p.CreatePaymentLinkAsync(It.IsAny<long>(), 100m, It.IsAny<string>()))
                .ReturnsAsync("https://checkout.payos.vn/payment-link");

            var expectedDto = new OrderDTO { OrderID = Guid.NewGuid() };
            _mockMapper
                .Setup(m => m.Map<OrderDTO>(It.IsAny<Order>()))
                .Returns(expectedDto);

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

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockOrderRepository.Verify(r => r.Add(It.Is<Order>(o =>
                o.StudentID == studentId &&
                o.CourseID == courseId &&
                o.PlatformAmount == 15m && // 100 * 15% = 15
                o.TeacherAmount == 85m
            )), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
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

            // Mock Coupons:
            // 1. Coupon hợp lệ của học sinh này (giảm 10)
            var validCoupon = new Coupon(Guid.NewGuid(), studentId, "COUPON1", 10m, "Reason 1");
            // 2. Coupon đã dùng (không được áp dụng tiếp)
            var usedCoupon = new Coupon(Guid.NewGuid(), studentId, "COUPON2", 15m, "Reason 2");
            usedCoupon.MarkAsUsed();
            // 3. Coupon của học sinh khác (không được áp dụng)
            var otherUserCoupon = new Coupon(Guid.NewGuid(), otherStudentId, "COUPON3", 20m, "Reason 3");

            _mockOrderRepository
                .Setup(r => r.GetCouponDetailById(validCoupon.CouponID))
                .ReturnsAsync(validCoupon);
            _mockOrderRepository
                .Setup(r => r.GetCouponDetailById(usedCoupon.CouponID))
                .ReturnsAsync(usedCoupon);
            _mockOrderRepository
                .Setup(r => r.GetCouponDetailById(otherUserCoupon.CouponID))
                .ReturnsAsync(otherUserCoupon);

            // Giá sau giảm: 100 - 10 = 90
            _mockPaymentService
                .Setup(p => p.CreatePaymentLinkAsync(It.IsAny<long>(), 90m, It.IsAny<string>()))
                .ReturnsAsync("https://checkout.payos.vn/payment-link-discount");

            var expectedDto = new OrderDTO { OrderID = Guid.NewGuid() };
            _mockMapper
                .Setup(m => m.Map<OrderDTO>(It.IsAny<Order>()))
                .Returns(expectedDto);

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

            // Chỉ coupon hợp lệ mới bị chuyển trạng thái thành Đã Dùng
            validCoupon.IsUsed.Should().BeTrue();

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockOrderRepository.Verify(r => r.Add(It.Is<Order>(o =>
                o.StudentID == studentId &&
                o.CourseID == courseId &&
                o.PlatformAmount == 13.5m && // 90 * 15% = 13.5
                o.TeacherAmount == 76.5m
            )), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
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
