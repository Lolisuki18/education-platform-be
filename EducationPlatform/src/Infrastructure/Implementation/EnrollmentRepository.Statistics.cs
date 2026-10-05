using Infrastructure.Persistence;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    /// <summary>The enrollment statistics of the repository (read-only aggregations for the dashboards).</summary>
    public partial class EnrollmentRepository
    {
        #region Statistics
        public async Task<(
           int Total,
           int Completed,
           Dictionary<string, int> GradeCounts,
           Dictionary<string, int> SubjectCounts
       )> Summary(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
        {
            // ===== Base query with filters =====
            var query = context.Enrollments.AsQueryable();

            if (from.HasValue)
                query = query.Where(e => e.EnrolledAt >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.EnrolledAt <= to.Value);

            // ===== 1. Basic summary =====
            var summary = await query
                .GroupBy(e => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Completed = g.Count(e => e.CompletedAt != null)
                })
                .FirstOrDefaultAsync(cancellationToken);

            // ===== 2. Grade distribution =====
            var gradeDict = await query
                .GroupBy(e => e.Course.Grade.Name)
                .Select(g => new
                {
                    Grade = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Grade, x => x.Count, cancellationToken);

            // ===== 3. Subject distribution =====
            var subjectDict = await query
                .GroupBy(e => e.Course.Subject.Name)
                .Select(g => new
                {
                    Subject = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Subject, x => x.Count, cancellationToken);

            // ===== Return =====
            return (
                summary?.Total ?? 0,
                summary?.Completed ?? 0,
                gradeDict,
                subjectDict
            );
        }

        public async Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            Guid? gradeId,
            Guid? subjectId,
            CancellationToken cancellationToken = default)
        {
            var query = context.Enrollments.AsQueryable();

            // ===== Filters =====
            if (from.HasValue)
                query = query.Where(e => e.EnrolledAt >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.EnrolledAt <= to.Value);

            if (gradeId.HasValue)
                query = query.Where(e => e.Course.GradeID == gradeId);

            if (subjectId.HasValue)
                query = query.Where(e => e.Course.SubjectID == subjectId);

            // Normalize groupBy
            var gb = (groupBy ?? "month").ToLower();

            // ===== Step 1: Dynamic grouping in DB =====
            var rawData = gb switch
            {
                "day" => await query
                    .GroupBy(e => new
                    {
                        e.EnrolledAt.Year,
                        e.EnrolledAt.Month,
                        e.EnrolledAt.Day
                    })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        g.Key.Day,
                        Count = g.Count()
                    })
                    .ToListAsync(cancellationToken),

                "month" => await query
                    .GroupBy(e => new
                    {
                        e.EnrolledAt.Year,
                        e.EnrolledAt.Month
                    })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Day = 0,
                        Count = g.Count()
                    })
                    .ToListAsync(cancellationToken),

                "year" => await query
                    .GroupBy(e => new
                    {
                        e.EnrolledAt.Year
                    })
                    .Select(g => new
                    {
                        g.Key.Year,
                        Month = 0,
                        Day = 0,
                        Count = g.Count()
                    })
                    .ToListAsync(cancellationToken),

                _ => throw new ArgumentException("Invalid groupBy")
            };

            // ===== Step 2: Format labels in memory =====
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
                    x.Count,
                    x.Year,
                    x.Month,
                    x.Day
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ThenBy(x => x.Day)
                .ToList();

            // ===== Build result =====
            return new Dictionary<string, List<(string, decimal)>>
            {
                {
                    "Enrollments",
                    data.Select(x => (x.Label, (decimal)x.Count)).ToList()
                }
            };
        }

        public async Task<List<(Guid CourseId, string CourseName, decimal EnrollmentCount)>>
    GetTopCoursesByEnrollment(
        DateTime? from,
        DateTime? to,
        Guid? gradeId,
        Guid? subjectId,
        int top,
        CancellationToken cancellationToken = default)
        {
            var q = context.Enrollments
                .Include(e => e.Course)
                .AsQueryable();

            if (from.HasValue)
                q = q.Where(e => e.EnrolledAt >= from.Value);

            if (to.HasValue)
                q = q.Where(e => e.EnrolledAt <= to.Value);

            if (gradeId.HasValue)
                q = q.Where(e => e.Course.GradeID == gradeId.Value);

            if (subjectId.HasValue)
                q = q.Where(e => e.Course.SubjectID == subjectId.Value);

            var result = await q
                .GroupBy(e => new { e.CourseID, e.Course.Title })
                .Select(g => new
                {
                    g.Key.CourseID,
                    CourseName = g.Key.Title,
                    EnrollmentCount = g.Count()
                })
                .OrderByDescending(x => x.EnrollmentCount)
                .Take(top)
                .ToListAsync(cancellationToken);

            return result
                .Select(x => (x.CourseID, x.CourseName, (decimal)x.EnrollmentCount))
                .ToList();
        }

        public async Task<List<(Guid SubjectId, string SubjectName, decimal EnrollmentCount)>>
            GetTopSubjectsByEnrollment(
                DateTime? from,
                DateTime? to,
                Guid? gradeId,
                int top,
                CancellationToken cancellationToken = default)
        {
            var query = context.Enrollments
                .Include(e => e.Course)
                .ThenInclude(c => c.Subject)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(e => e.EnrolledAt >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.EnrolledAt <= to.Value);

            if (gradeId.HasValue)
                query = query.Where(e => e.Course.GradeID == gradeId.Value);

            var result = await query
                .GroupBy(e => new
                {
                    e.Course.SubjectID,
                    SubjectName = e.Course.Subject.Name
                })
                .Select(g => new
                {
                    g.Key.SubjectID,
                    g.Key.SubjectName,
                    EnrollmentCount = g.Count()
                })
                .OrderByDescending(x => x.EnrollmentCount)
                .Take(top)
                .ToListAsync(cancellationToken);

            return result
                .Select(x => (x.SubjectID, x.SubjectName, (decimal)x.EnrollmentCount))
                .ToList();
        }

        public async Task<List<(Guid GradeId, string GradeName, decimal EnrollmentCount)>>
            GetTopGradesByEnrollment(
                DateTime? from,
                DateTime? to,
                Guid? subjectId,
                int top,
                CancellationToken cancellationToken = default)
        {
            var query = context.Enrollments
                .Include(e => e.Course)
                .ThenInclude(c => c.Grade)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(e => e.EnrolledAt >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.EnrolledAt <= to.Value);

            if (subjectId.HasValue)
                query = query.Where(e => e.Course.SubjectID == subjectId.Value);

            var result = await query
                .GroupBy(e => new
                {
                    e.Course.GradeID,
                    GradeName = e.Course.Grade.Name
                })
                .Select(g => new
                {
                    g.Key.GradeID,
                    g.Key.GradeName,
                    EnrollmentCount = g.Count()
                })
                .OrderByDescending(x => x.EnrollmentCount)
                .Take(top)
                .ToListAsync(cancellationToken);

            return result
                .Select(x => (x.GradeID, x.GradeName, (decimal)x.EnrollmentCount))
                .ToList();
        }
        #endregion
    }
}
