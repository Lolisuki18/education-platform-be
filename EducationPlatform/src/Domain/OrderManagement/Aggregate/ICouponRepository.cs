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
            CouponType? type,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

        Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    }
}
