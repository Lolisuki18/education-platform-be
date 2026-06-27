using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class OrderRepository :
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
            Guid? studentId)
        {
            pageIndex = pageIndex < 1 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            IQueryable<Order> query = context.Orders
                .AsNoTracking();

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
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<Order?> GetOrderByOrderCode(
            long orderCode)
        {
            return await context.Orders.FirstOrDefaultAsync(o => o.OrderCode == orderCode);
        }

        public async Task<IEnumerable<Penalty>> GetPenalties(
            Guid? teacherId)
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
                .ToListAsync();
        }

        public void CreatePenalty(
            Penalty penalty)
        {
            if (penalty == null)
                return;

            context.Penalties.Add(penalty);
        }

        public async Task<IEnumerable<Coupon>> GetCoupons(
            Guid? studentId)
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
                .ToListAsync();
        }

        public async Task<Coupon?> GetCouponDetailById(
            Guid couponId)
        {
            return await context.Coupons
                .FirstOrDefaultAsync(c => c.CouponID == couponId);
        }

        public void CreateCoupons(IEnumerable<Coupon> coupons)
        {
            if (coupons == null || !coupons.Any())
                return;

            context.Coupons.AddRange(coupons);
        }
        public async Task<(int Total, int Commission, int TeacherFinance)> Summary(
           DateTime? from,
           DateTime? to)
        {
            // ===== Base query with filters =====
            var query = context.Orders.AsQueryable();

            if (from.HasValue)
                query = query.Where(o => o.PaidAt >= from.Value);

            if (to.HasValue)
                query = query.Where(o => o.PaidAt <= to.Value);

            var result = await query
                .GroupBy(o => 1)
                .Select(g => new
                {
                    Total = g.Sum(x => (int)(x.PlatformAmount + x.TeacherAmount)),
                    Commission = g.Sum(x => (int)x.PlatformAmount),
                    TeacherFinance = g.Sum(x => (int)x.TeacherAmount)
                })
                .FirstOrDefaultAsync();

            return result == null
                ? (0, 0, 0)
                : (result.Total, result.Commission, result.TeacherFinance);
        }

        public async Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            string revenueType)
        {
            var query = context.Orders.AsQueryable();

            // ===== Filters =====
            if (from.HasValue)
                query = query.Where(o => o.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(o => o.CreatedAt <= to.Value);

            var gb = (groupBy ?? "month").ToLower();

            // ===== Step 1: Dynamic grouping =====
            var rawData = gb switch
            {
                "day" => await query
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        g.Key.Day,
                        Commission = g.Sum(x => x.PlatformAmount),
                        Teacher = g.Sum(x => x.TeacherAmount)
                    })
                    .ToListAsync(),

                "month" => await query
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Day = 0,
                        Commission = g.Sum(x => x.PlatformAmount),
                        Teacher = g.Sum(x => x.TeacherAmount)
                    })
                    .ToListAsync(),

                "year" => await query
                    .GroupBy(o => new { o.CreatedAt.Year })
                    .Select(g => new
                    {
                        g.Key.Year,
                        Month = 0,
                        Day = 0,
                        Commission = g.Sum(x => x.PlatformAmount),
                        Teacher = g.Sum(x => x.TeacherAmount)
                    })
                    .ToListAsync(),

                _ => throw new ArgumentException("Invalid groupBy")
            };

            // ===== Step 2: Format labels =====
            var data = rawData
                .Select(x => new
                {
                    Label = gb switch
                    {
                        "day" => $"{x.Year}-{x.Month:D2}-{x.Day:D2}",
                        "month" => $"{x.Year}-{x.Month:D2}",
                        "year" => x.Year.ToString(),
                        _ => $"{x.Year}-{x.Month:D2}"
                    },
                    x.Commission,
                    x.Teacher,
                    x.Year,
                    x.Month,
                    x.Day
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ThenBy(x => x.Day)
                .ToList();

            // ===== Build result =====
            var result = new Dictionary<string, List<(string, decimal)>>();

            if (revenueType == "All" || revenueType == "Commission")
            {
                result["Commission"] = data
                    .Select(x => (x.Label, x.Commission))
                    .ToList();
            }

            if (revenueType == "All" || revenueType == "Teacher")
            {
                result["Teacher"] = data
                    .Select(x => (x.Label, x.Teacher))
                    .ToList();
            }

            return result;
        }

        public async Task<List<(Guid CourseId, string CourseName, decimal Revenue)>>
            GetTopCoursesByRevenue(
                DateTime? from,
                DateTime? to,
                Guid? gradeId,
                Guid? subjectId,
                int top)
        {
            var query = context.Orders
                .Include(o => o.Course)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(o => o.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(o => o.CreatedAt <= to.Value);

            if (gradeId.HasValue)
                query = query.Where(o => o.Course.GradeID == gradeId.Value);

            if (subjectId.HasValue)
                query = query.Where(o => o.Course.SubjectID == subjectId.Value);

            var result = await query
                .GroupBy(o => new
                {
                    o.CourseID,
                    CourseName = o.Course.Title
                })
                .Select(g => new
                {
                    g.Key.CourseID,
                    g.Key.CourseName,
                    Revenue = g.Sum(x => x.PlatformAmount)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(top)
                .ToListAsync();

            return result
                .Select(x => (x.CourseID, x.CourseName, (decimal)x.Revenue))
                .ToList();
        }

        public async Task<List<(Guid SubjectId, string SubjectName, decimal Revenue)>>
            GetTopSubjectsByRevenue(
                DateTime? from,
                DateTime? to,
                Guid? gradeId,
                int top)
        {
            var query = context.Orders
                .Include(o => o.Course)
                .ThenInclude(c => c.Subject)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(o => o.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(o => o.CreatedAt <= to.Value);

            if (gradeId.HasValue)
                query = query.Where(o => o.Course.GradeID == gradeId.Value);

            var result = await query
                .GroupBy(o => new
                {
                    o.Course.SubjectID,
                    SubjectName = o.Course.Subject.Name
                })
                .Select(g => new
                {
                    g.Key.SubjectID,
                    g.Key.SubjectName,
                    Revenue = g.Sum(x => x.PlatformAmount)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(top)
                .ToListAsync();

            return result
                .Select(x => (x.SubjectID, x.SubjectName, (decimal)x.Revenue))
                .ToList();
        }

        public async Task<List<(Guid GradeId, string GradeName, decimal Revenue)>>
            GetTopGradesByRevenue(
                DateTime? from,
                DateTime? to,
                Guid? subjectId,
                int top)
        {
            var query = context.Orders
                .Include(o => o.Course)
                .ThenInclude(c => c.Grade)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(o => o.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(o => o.CreatedAt <= to.Value);

            if (subjectId.HasValue)
                query = query.Where(o => o.Course.SubjectID == subjectId.Value);

            var result = await query
                .GroupBy(o => new
                {
                    o.Course.GradeID,
                    GradeName = o.Course.Grade.Name
                })
                .Select(g => new
                {
                    g.Key.GradeID,
                    g.Key.GradeName,
                    Revenue = g.Sum(x => x.PlatformAmount)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(top)
                .ToListAsync();

            return result
                .Select(x => (x.GradeID, x.GradeName, (decimal)x.Revenue))
                .ToList();
        }
        #endregion
    }
}

