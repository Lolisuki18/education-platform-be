using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Implementation
{
    public class CouponRepository : GenericRepository<Coupon>, ICouponRepository
    {
        public CouponRepository(EducationPlatformDBContext context) : base(context)
        {
        }

        public async Task<(IEnumerable<Coupon> Coupons, int TotalCount)> GetPagedAsync(
            int pageIndex,
            int pageSize,
            string? search,
            bool? isActive,
            CouponType? type,
            CancellationToken cancellationToken = default)
        {
            var query = context.Coupons.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var cleanSearch = search.Trim().ToLower();
                query = query.Where(c => c.Code.ToLower().Contains(cleanSearch)
                                      || c.Description.ToLower().Contains(cleanSearch));
            }

            if (isActive.HasValue)
            {
                query = query.Where(c => c.IsActive == isActive.Value);
            }

            if (type.HasValue)
            {
                query = query.Where(c => c.Type == type.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var list = await query
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (list, totalCount);
        }

        public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            var normalizedCode = code.Trim().ToUpper();
            return await context.Coupons.AnyAsync(c => c.Code == normalizedCode, cancellationToken);
        }

        public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var normalizedCode = code.Trim().ToUpper();
            return await context.Coupons.FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);
        }
    }
}
