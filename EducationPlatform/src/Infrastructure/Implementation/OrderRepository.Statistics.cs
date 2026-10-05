using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    /// <summary>The revenue statistics of the repository (read-only aggregations for the dashboards).</summary>
    public partial class OrderRepository
    {
        #region Statistics
        public async Task<(int Total, int Commission, int TeacherFinance)> Summary(
           DateTime? from,
           DateTime? to,
           CancellationToken cancellationToken = default)
        {
            // ===== Base query with filters =====
            var query = context.Orders.AsNoTracking();

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
                .FirstOrDefaultAsync(cancellationToken);

            return result == null
                ? (0, 0, 0)
                : (result.Total, result.Commission, result.TeacherFinance);
        }

        public async Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            string revenueType,
            CancellationToken cancellationToken = default)
        {
            var query = context.Orders.AsNoTracking();

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
                    .ToListAsync(cancellationToken),

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
                    .ToListAsync(cancellationToken),

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
                    .ToListAsync(cancellationToken),

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
                int top,
                CancellationToken cancellationToken = default)
        {
            var query = context.Orders
                .AsNoTracking()
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
                .ToListAsync(cancellationToken);

            return result
                .Select(x => (x.CourseID, x.CourseName, (decimal)x.Revenue))
                .ToList();
        }

        public async Task<List<(Guid SubjectId, string SubjectName, decimal Revenue)>>
            GetTopSubjectsByRevenue(
                DateTime? from,
                DateTime? to,
                Guid? gradeId,
                int top,
                CancellationToken cancellationToken = default)
        {
            var query = context.Orders
                .AsNoTracking()
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
                .ToListAsync(cancellationToken);

            return result
                .Select(x => (x.SubjectID, x.SubjectName, (decimal)x.Revenue))
                .ToList();
        }

        public async Task<List<(Guid GradeId, string GradeName, decimal Revenue)>>
            GetTopGradesByRevenue(
                DateTime? from,
                DateTime? to,
                Guid? subjectId,
                int top,
                CancellationToken cancellationToken = default)
        {
            var query = context.Orders
                .AsNoTracking()
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
                .ToListAsync(cancellationToken);

            return result
                .Select(x => (x.GradeID, x.GradeName, (decimal)x.Revenue))
                .ToList();
        }
        #endregion
    }
}
