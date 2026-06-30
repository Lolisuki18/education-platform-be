using Domain.Common.Interfaces;
using Domain.OrderManagement.Enum;

namespace Domain.OrderManagement.Aggregate
{
    public interface ICouponRepository : IGenericRepository<Coupon>
    {
        Task<(IEnumerable<Coupon> Coupons, int TotalCount)> GetPagedAsync(
            int pageIndex,
            int pageSize,
            string? search,
            bool? isActive,
            CouponType? type);

        Task<bool> ExistsByCodeAsync(string code);

        Task<Coupon?> GetByCodeAsync(string code);
    }
}
