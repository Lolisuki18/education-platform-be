using Application.BusinessException;
using Application.Features.Orders.Queries.GetCoupons;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.ValueObject;
using Domain.OrderManagement.Aggregate;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Orders.Queries.GetCoupons
{
    public class GetCouponsQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly GetCouponsQueryHandler _handler;

        public GetCouponsQueryHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IOrderRepository>())
                .Returns(_mockOrderRepository.Object);

            _handler = new GetCouponsQueryHandler(
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
            var query = new GetCouponsQuery();

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
            var query = new GetCouponsQuery();

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("Invalid role");
        }

        [Fact]
        public async Task Handle_StudentRole_ShouldScopeByStudentId()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);
            _mockCurrentUser.Setup(u => u.Role).Returns("Student");

            var coupons = new List<Coupon>
            {
                new Coupon(Guid.NewGuid(), studentId, "CODE1", 10m, "Reason 1")
            };

            _mockOrderRepository
                .Setup(r => r.GetCoupons(studentId))
                .ReturnsAsync(coupons);

            var expectedDtos = new List<CouponDTO>
            {
                new CouponDTO { CouponID = coupons[0].CouponID, Code = "CODE1", DiscountAmount = 10m }
            };

            _mockMapper
                .Setup(m => m.Map<IEnumerable<CouponDTO>>(coupons))
                .Returns(expectedDtos);

            var query = new GetCouponsQuery();

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeEquivalentTo(expectedDtos);
            _mockOrderRepository.Verify(r => r.GetCoupons(studentId), Times.Once);
        }

        [Fact]
        public async Task Handle_NonStudentRole_ShouldNotScopeByStudentId()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(adminId);
            _mockCurrentUser.Setup(u => u.Role).Returns("Admin");

            var coupons = new List<Coupon>
            {
                new Coupon(Guid.NewGuid(), Guid.NewGuid(), "CODE1", 10m, "Reason 1"),
                new Coupon(Guid.NewGuid(), Guid.NewGuid(), "CODE2", 15m, "Reason 2")
            };

            _mockOrderRepository
                .Setup(r => r.GetCoupons(null))
                .ReturnsAsync(coupons);

            var expectedDtos = new List<CouponDTO>
            {
                new CouponDTO { CouponID = coupons[0].CouponID, Code = "CODE1", DiscountAmount = 10m },
                new CouponDTO { CouponID = coupons[1].CouponID, Code = "CODE2", DiscountAmount = 15m }
            };

            _mockMapper
                .Setup(m => m.Map<IEnumerable<CouponDTO>>(coupons))
                .Returns(expectedDtos);

            var query = new GetCouponsQuery();

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeEquivalentTo(expectedDtos);
            _mockOrderRepository.Verify(r => r.GetCoupons(null), Times.Once);
        }
    }
}
