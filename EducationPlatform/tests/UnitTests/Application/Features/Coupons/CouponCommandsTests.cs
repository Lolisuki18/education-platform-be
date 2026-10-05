using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Features.Coupons.Commands.CreateCoupon;
using Application.Features.Coupons.Commands.UpdateCoupon;
using Application.Features.Coupons.Commands.UpdateCouponStatus;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.OrderManagement.Aggregate;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Coupons
{
    public class CouponCommandsTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICouponRepository> _mockCouponRepository;
        private readonly Mock<ICurrentUser> _mockCurrentUser;

        public CouponCommandsTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCouponRepository = new Mock<ICouponRepository>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<ICouponRepository>())
                .Returns(_mockCouponRepository.Object);
        }

        // ======================= CREATE COUPON =======================

        [Fact]
        public async Task CreateCouponCommand_ShouldPassCurrentUserIdToUnitOfWork()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            _mockCouponRepository
                .Setup(r => r.ExistsByCodeAsync("WINTER2026"))
                .ReturnsAsync(false);

            var command = new CreateCouponCommand
            {
                Code = "WINTER2026",
                Description = "Winter Discount",
                DiscountAmount = 25m,
                StartDate = DateTime.UtcNow.AddDays(1),
                ExpiredDate = DateTime.UtcNow.AddDays(10),
                MaxUsage = 100
            };

            var handler = new CreateCouponCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeEmpty();
            _mockCouponRepository.Verify(r => r.Add(It.IsAny<Coupon>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(currentUserId.ToString()), Times.Once);
        }

        [Fact]
        public async Task CreateCouponCommand_DuplicateCode_ShouldThrowConflictException()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            _mockCouponRepository
                .Setup(r => r.ExistsByCodeAsync("DUPLICATE"))
                .ReturnsAsync(true);

            var command = new CreateCouponCommand { Code = "DUPLICATE", DiscountAmount = 10m };
            var handler = new CreateCouponCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ConflictException>().WithMessage("*already exists*");
            _mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<string>()), Times.Never);
        }

        // ======================= UPDATE COUPON =======================

        [Fact]
        public async Task UpdateCouponCommand_ShouldPassCurrentUserIdToUnitOfWork()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var couponId = Guid.NewGuid();
            var coupon = new Coupon(couponId, "SPRING50", "Spring Sale", 50m, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(5), 50);

            _mockCouponRepository
                .Setup(r => r.GetByIdAsync(couponId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(coupon);

            var command = new UpdateCouponCommand
            {
                CouponId = couponId,
                Description = "Updated spring sale",
                DiscountAmount = 45m,
                StartDate = DateTime.UtcNow.AddDays(2),
                ExpiredDate = DateTime.UtcNow.AddDays(8),
                MaxUsage = 60
            };

            var handler = new UpdateCouponCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            coupon.Description.Should().Be("Updated spring sale");
            coupon.DiscountAmount.Should().Be(45m);
            coupon.MaxUsage.Should().Be(60);
            _mockCouponRepository.Verify(r => r.UpdateAsync(couponId, coupon, It.IsAny<CancellationToken>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(currentUserId.ToString()), Times.Once);
        }

        [Fact]
        public async Task UpdateCouponCommand_NotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var couponId = Guid.NewGuid();
            _mockCouponRepository
                .Setup(r => r.GetByIdAsync(couponId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Coupon?)null);

            var command = new UpdateCouponCommand { CouponId = couponId };
            var handler = new UpdateCouponCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Coupon not found.");
            _mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<string>()), Times.Never);
        }

        // ======================= UPDATE COUPON STATUS =======================

        [Fact]
        public async Task UpdateCouponStatusCommand_ShouldPassCurrentUserIdToUnitOfWork()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var couponId = Guid.NewGuid();
            var coupon = new Coupon(couponId, "STATUSCODE", "Desc", 10m, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(5), 10);
            coupon.Deactivate();

            _mockCouponRepository
                .Setup(r => r.GetByIdAsync(couponId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(coupon);

            var command = new UpdateCouponStatusCommand { CouponId = couponId, IsActive = true };
            var handler = new UpdateCouponStatusCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            coupon.IsActive.Should().BeTrue();
            _mockCouponRepository.Verify(r => r.UpdateAsync(couponId, coupon, It.IsAny<CancellationToken>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(currentUserId.ToString()), Times.Once);
        }

        [Fact]
        public async Task UpdateCouponStatusCommand_NotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var couponId = Guid.NewGuid();
            _mockCouponRepository
                .Setup(r => r.GetByIdAsync(couponId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Coupon?)null);

            var command = new UpdateCouponStatusCommand { CouponId = couponId, IsActive = true };
            var handler = new UpdateCouponStatusCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Coupon not found.");
        }

        [Fact]
        public async Task UpdateCouponStatusCommand_ExpiredReactivation_ShouldThrowConflictException()
        {
            // Arrange
            var currentUserId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(currentUserId);

            var couponId = Guid.NewGuid();
            // Expired coupon
            var coupon = new Coupon(couponId, "EXPIRED", "Desc", 10m, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddSeconds(1), 10);
            coupon.Deactivate();

            System.Threading.Thread.Sleep(1500); // Wait for expiration

            _mockCouponRepository
                .Setup(r => r.GetByIdAsync(couponId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(coupon);

            var command = new UpdateCouponStatusCommand { CouponId = couponId, IsActive = true };
            var handler = new UpdateCouponStatusCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ConflictException>().WithMessage("Expired coupons cannot be reactivated.");
            _mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
