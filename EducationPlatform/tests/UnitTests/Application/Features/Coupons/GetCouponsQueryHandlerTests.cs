using Application.Exceptions;
using Application.Features.Coupons.Queries.GetCoupons;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Coupons
{
    public class GetCouponsQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<ICouponRepository> _coupons = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<ICurrentUser> _currentUser = new();
        private readonly GetCouponsQueryHandler _handler;

        public GetCouponsQueryHandlerTests()
        {
            _unitOfWork.Setup(u => u.GetRepository<ICouponRepository>()).Returns(_coupons.Object);
            _currentUser.Setup(c => c.Id).Returns(Guid.NewGuid());
            _handler = new GetCouponsQueryHandler(_unitOfWork.Object, _mapper.Object, _currentUser.Object);
        }

        [Fact]
        public async Task PassesTheFiltersToTheRepository_AndBuildsThePage()
        {
            var found = new List<Coupon> { new(Guid.NewGuid(), Guid.NewGuid(), "SORRY-1", 10m, "Compensation") };
            _coupons
                .Setup(c => c.GetPagedAsync(2, 5, "sale", true, CouponType.Marketing, It.IsAny<CancellationToken>()))
                .ReturnsAsync((found, 12));
            _mapper.Setup(m => m.Map<IEnumerable<CouponDTO>>(found))
                .Returns(new List<CouponDTO> { new() { Code = "SORRY-1" } });

            var result = await _handler.Handle(
                new GetCouponsQuery { PageIndex = 2, PageSize = 5, Search = "sale", IsActive = true, Type = CouponType.Marketing },
                CancellationToken.None);

            result.Items.Should().ContainSingle().Which.Code.Should().Be("SORRY-1");
            result.PageIndex.Should().Be(2);
            result.PageSize.Should().Be(5);
            result.TotalItems.Should().Be(12);
        }

        [Fact]
        public async Task NoCoupons_GiveAnEmptyPage()
        {
            _coupons
                .Setup(c => c.GetPagedAsync(1, 10, null, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<Coupon>(), 0));
            _mapper.Setup(m => m.Map<IEnumerable<CouponDTO>>(It.IsAny<object>())).Returns(new List<CouponDTO>());

            var result = await _handler.Handle(new GetCouponsQuery(), CancellationToken.None);

            result.Items.Should().BeEmpty();
            result.TotalItems.Should().Be(0);
        }

        [Fact]
        public async Task WithoutSignIn_IsRefusedBeforeAnythingIsRead()
        {
            _currentUser.Setup(c => c.Id).Returns((Guid?)null);

            var act = () => _handler.Handle(new GetCouponsQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
            _coupons.Verify(c => c.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<CouponType?>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
