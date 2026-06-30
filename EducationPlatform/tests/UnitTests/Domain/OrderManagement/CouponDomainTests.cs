using Domain.DomainExceptions;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using FluentAssertions;
using System;
using Xunit;

namespace UnitTests.DomainTests.OrderManagement
{
    public class CouponDomainTests
    {
        [Fact]
        public void CouponConstructor_Admin_ValidArguments_ShouldCreateSuccessfully()
        {
            var couponId = Guid.NewGuid();
            var startDate = DateTime.UtcNow.AddDays(1);
            var expiredDate = DateTime.UtcNow.AddDays(10);
            var maxUsage = 100;

            var coupon = new Coupon(couponId, "WINTER50", "Winter discount", 50m, startDate, expiredDate, maxUsage);

            coupon.CouponID.Should().Be(couponId);
            coupon.StudentID.Should().BeNull();
            coupon.Code.Should().Be("WINTER50");
            coupon.Description.Should().Be("Winter discount");
            coupon.DiscountAmount.Should().Be(50m);
            coupon.StartDate.Should().Be(startDate);
            coupon.ExpiredDate.Should().Be(expiredDate);
            coupon.MaxUsage.Should().Be(maxUsage);
            coupon.CurrentUsage.Should().Be(0);
            coupon.IsActive.Should().BeTrue();
            coupon.Type.Should().Be(CouponType.Marketing);
            coupon.Version.Should().Be(1);
        }

        [Theory]
        [InlineData("W", "Code length must be between 3 and 50 characters")]
        [InlineData("Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "Code length must be between 3 and 50 characters")]
        public void CouponConstructor_Admin_InvalidCodeLength_ShouldThrowDomainException(string code, string expectedError)
        {
            Action act = () => new Coupon(Guid.NewGuid(), code, "Desc", 10m, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 10);
            act.Should().Throw<DomainException>().WithMessage($"*{expectedError}*");
        }

        [Fact]
        public void CouponConstructor_Admin_StartDateAfterExpiredDate_ShouldThrowDomainException()
        {
            Action act = () => new Coupon(Guid.NewGuid(), "CODE", "Desc", 10m, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(1), 10);
            act.Should().Throw<DomainException>().WithMessage("Start date must be earlier than expiration date");
        }

        [Fact]
        public void CouponConstructor_Admin_ExpiredDateInPast_ShouldThrowDomainException()
        {
            Action act = () => new Coupon(Guid.NewGuid(), "CODE", "Desc", 10m, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), 10);
            act.Should().Throw<DomainException>().WithMessage("Coupon expiration date must be greater than current UTC time");
        }

        [Fact]
        public void Coupon_UpdateDetails_ValidArguments_ShouldUpdateDetailsAndIncrementVersion()
        {
            var coupon = new Coupon(Guid.NewGuid(), "WINTER50", "Winter discount", 50m, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(10), 100);
            var newStartDate = DateTime.UtcNow.AddDays(2);
            var newExpiredDate = DateTime.UtcNow.AddDays(15);

            coupon.UpdateDetails("New Desc", 40m, newStartDate, newExpiredDate, 150);

            coupon.Description.Should().Be("New Desc");
            coupon.DiscountAmount.Should().Be(40m);
            coupon.StartDate.Should().Be(newStartDate);
            coupon.ExpiredDate.Should().Be(newExpiredDate);
            coupon.MaxUsage.Should().Be(150);
            coupon.Version.Should().Be(2);
            coupon.UpdatedAt.Should().NotBeNull();
        }

        [Fact]
        public void Coupon_Activate_ValidCoupon_ShouldActivateAndIncrementVersion()
        {
            var coupon = new Coupon(Guid.NewGuid(), "CODE", "Desc", 10m, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(5), 10);
            coupon.Deactivate(); // Version becomes 2
            coupon.IsActive.Should().BeFalse();

            coupon.Activate(); // Version becomes 3

            coupon.IsActive.Should().BeTrue();
            coupon.Version.Should().Be(3);
        }

        [Fact]
        public void Coupon_Activate_ExpiredCoupon_ShouldThrowDomainException()
        {
            // We set the expired date to 1 second from now, then wait/simulate expired
            // Actually, we can just construct a valid coupon with a future expiration date, update its state or use reflection.
            // But we can also set the expiration date to be very short, or test reactivating after it has expired.
            // Let's create a coupon that will expire:
            var coupon = new Coupon(Guid.NewGuid(), "CODE", "Desc", 10m, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddSeconds(1), 10);

            // Wait 1.5 seconds for it to expire
            System.Threading.Thread.Sleep(1500);

            Action act = () => coupon.Activate();
            act.Should().Throw<DomainException>().WithMessage("Expired coupons cannot be reactivated.");
        }

        [Fact]
        public void Coupon_Deactivate_ShouldDeactivateAndIncrementVersion()
        {
            var coupon = new Coupon(Guid.NewGuid(), "CODE", "Desc", 10m, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(5), 10);
            coupon.IsActive.Should().BeTrue();

            coupon.Deactivate();

            coupon.IsActive.Should().BeFalse();
            coupon.Version.Should().Be(2);
        }

        [Fact]
        public void Coupon_CanBeApplied_ActiveAndValid_ShouldReturnTrue()
        {
            var coupon = new Coupon(Guid.NewGuid(), "CODE", "Desc", 10m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(5), 10);
            coupon.CanBeApplied().Should().BeTrue();
        }

        [Fact]
        public void Coupon_CanBeApplied_Inactive_ShouldReturnFalse()
        {
            var coupon = new Coupon(Guid.NewGuid(), "CODE", "Desc", 10m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(5), 10);
            coupon.Deactivate();
            coupon.CanBeApplied().Should().BeFalse();
        }

        [Fact]
        public void Coupon_CanBeApplied_Expired_ShouldReturnFalse()
        {
            var coupon = new Coupon(Guid.NewGuid(), "CODE", "Desc", 10m, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddSeconds(1), 10);
            System.Threading.Thread.Sleep(1500);
            coupon.CanBeApplied().Should().BeFalse();
        }

        [Fact]
        public void Coupon_CanBeApplied_UsageExceeded_ShouldReturnFalse()
        {
            var coupon = new Coupon(Guid.NewGuid(), "CODE", "Desc", 10m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(5), 1);
            coupon.MarkAsUsed();
            coupon.CanBeApplied().Should().BeFalse();
        }

        [Fact]
        public void Coupon_MarkAsUsed_Valid_ShouldIncrementUsageAndVersion()
        {
            var coupon = new Coupon(Guid.NewGuid(), "CODE", "Desc", 10m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(5), 10);
            coupon.CurrentUsage.Should().Be(0);

            coupon.MarkAsUsed();

            coupon.CurrentUsage.Should().Be(1);
            coupon.IsUsed.Should().BeTrue();
            coupon.Version.Should().Be(2);
        }
    }
}
