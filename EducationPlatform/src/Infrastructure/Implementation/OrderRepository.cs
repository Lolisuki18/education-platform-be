using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public partial class OrderRepository :
        GenericRepository<Order>,
        IOrderRepository
    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        public OrderRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods
        public async Task<IEnumerable<Order>> GetOrders(
            string? status,
            int pageIndex,
            int pageSize,
            Guid? teacherId,
            Guid? studentId,
            CancellationToken cancellationToken = default)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            IQueryable<Order> query = context.Orders
                .AsNoTracking()
                .Include(o => o.Course);

            // ---- Status filter ----
            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(o => o.Status == parsedStatus);
            }

            // ---- Teacher filter (via Courses table) ----
            if (teacherId.HasValue)
            {
                query = query.Where(o =>
                    context.Courses.Any(c =>
                        c.CourseID == o.CourseID &&
                        c.TeacherID == teacherId.Value));
            }

            // ---- Student filter ----
            if (studentId.HasValue)
            {
                query = query.Where(o => o.StudentID == studentId.Value);
            }

            // ---- Sorting + paging ----
            return await query
                .OrderByDescending(o => o.PaidAt ?? DateTime.MinValue)
                // Unpaid orders all tie on PaidAt: without a tiebreaker a row can show up on two pages or on none
                .ThenByDescending(o => o.CreatedAt)
                .ThenBy(o => o.OrderID)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<Order?> GetOrderByOrderCode(
            long orderCode,
            CancellationToken cancellationToken = default)
        {
            return await context.Orders.FirstOrDefaultAsync(o => o.OrderCode == orderCode, cancellationToken);
        }

        public async Task<IEnumerable<Penalty>> GetPenalties(
            Guid? teacherId,
            CancellationToken cancellationToken = default)
        {
            var query = context.Penalties
                .AsNoTracking()
                .AsQueryable();

            if (teacherId.HasValue)
            {
                query = query.Where(p => p.TeacherID == teacherId.Value);
            }

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public void CreatePenalty(
            Penalty penalty)
        {
            if (penalty == null)
                return;

            context.Penalties.Add(penalty);
        }

        public async Task<IEnumerable<Coupon>> GetCoupons(
            Guid? studentId,
            CancellationToken cancellationToken = default)
        {
            var query = context.Coupons
                .AsNoTracking()
                .AsQueryable();

            if (studentId.HasValue)
            {
                query = query.Where(c => c.StudentID == studentId.Value);
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Coupon?> GetCouponDetailById(
            Guid couponId,
            CancellationToken cancellationToken = default)
        {
            return await context.Coupons
                .FirstOrDefaultAsync(c => c.CouponID == couponId, cancellationToken);
        }

        public async Task<List<Coupon>> GetCouponsByIds(
            IEnumerable<Guid> couponIds,
            CancellationToken cancellationToken = default)
        {
            var ids = couponIds.Distinct().ToList();
            if (ids.Count == 0)
                return new List<Coupon>();

            return await context.Coupons
                .Where(c => ids.Contains(c.CouponID))
                .ToListAsync(cancellationToken);
        }

        public async Task<Order?> GetAwaitingPaymentOrder(
            Guid studentId,
            Guid courseId,
            CancellationToken cancellationToken = default)
        {
            return await context.Orders
                .FirstOrDefaultAsync(o =>
                    o.StudentID == studentId &&
                    o.CourseID == courseId &&
                    o.Status == OrderStatus.Created, cancellationToken);
        }

        public async Task<bool> HasOpenOrderAsync(
            Guid studentId,
            DateTime now,
            CancellationToken cancellationToken = default)
        {
            var oldestOpen = now - Order.PaymentWindow;

            return await context.Orders
                .AsNoTracking()
                .AnyAsync(o =>
                    o.StudentID == studentId &&
                    o.Status == OrderStatus.Created &&
                    o.CreatedAt > oldestOpen, cancellationToken);
        }

        public async Task<List<Order>> GetUnpaidOrdersCreatedBefore(
            DateTime createdBefore,
            int take,
            CancellationToken cancellationToken = default)
        {
            return await context.Orders
                .Where(o => o.Status == OrderStatus.Created && o.CreatedAt < createdBefore)
                .OrderBy(o => o.CreatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public void CreateCoupons(IEnumerable<Coupon> coupons)
        {
            if (coupons == null || !coupons.Any())
                return;

            context.Coupons.AddRange(coupons);
        }

        #endregion
    }
}

