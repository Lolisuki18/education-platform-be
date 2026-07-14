using MediatR;
using Application.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.BusinessException;

namespace Application.Features.Statistics.Queries.GetTeacherSummary
{
    public class TeacherSummaryDTO
    {
        public int TotalCourses { get; set; }
        public int ActiveStudents { get; set; }
        public double AverageRating { get; set; }
        public decimal TotalRevenue { get; set; }
        public Dictionary<string, int> CourseStatusDistribution { get; set; } = new();
        public List<MonthlyRevenuePointDTO> MonthlyRevenue { get; set; } = new();
    }

    public class MonthlyRevenuePointDTO
    {
        public string Month { get; set; } = default!;
        public decimal Revenue { get; set; }
    }

    public class GetTeacherSummaryQuery : IRequest<TeacherSummaryDTO>
    {
    }

    public class GetTeacherSummaryQueryHandler : IRequestHandler<GetTeacherSummaryQuery, TeacherSummaryDTO>
    {
        private readonly IApplicationDBContext _context;
        private readonly ICurrentUser _currentUser;

        public GetTeacherSummaryQueryHandler(IApplicationDBContext context, ICurrentUser currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<TeacherSummaryDTO> Handle(GetTeacherSummaryQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var teacherId = _currentUser.Id.Value;

            // 1. Total Courses
            var totalCourses = await _context.Courses
                .AsNoTracking()
                .CountAsync(c => c.TeacherID == teacherId, cancellationToken);

            // 2. Active Students (Enrollments on courses created by this teacher)
            var activeStudents = await _context.Enrollments
                .AsNoTracking()
                .CountAsync(e => e.Course.TeacherID == teacherId, cancellationToken);

            // 3. Average Rating (from CourseReviews where the course belongs to the teacher)
            var reviewRatings = await _context.CourseReviews
                .AsNoTracking()
                .Where(r => r.Course.TeacherID == teacherId && r.DeleteAt == null)
                .Select(r => r.Rating)
                .ToListAsync(cancellationToken);

            double averageRating = reviewRatings.Any() ? Math.Round(reviewRatings.Average(), 1) : 0.0;

            // 4. Total Revenue (teacher's share from paid orders of their courses)
            var totalRevenue = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Course.TeacherID == teacherId && o.PaidAt != null)
                .SumAsync(o => o.TeacherAmount, cancellationToken);

            // 5. Course Status Distribution
            var courseStatusGroup = await _context.Courses
                .AsNoTracking()
                .Where(c => c.TeacherID == teacherId)
                .GroupBy(c => c.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var distribution = new Dictionary<string, int>
            {
                { "InReview", 0 },
                { "Published", 0 },
                { "Rejected", 0 }
            };

            foreach (var item in courseStatusGroup)
            {
                var statusStr = item.Status.ToString();
                if (distribution.ContainsKey(statusStr))
                {
                    distribution[statusStr] = item.Count;
                }
            }

            // 6. Monthly Revenue (for the last 6 months)
            var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
            var paidOrders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Course.TeacherID == teacherId && o.PaidAt != null && o.PaidAt >= sixMonthsAgo)
                .Select(o => new { o.PaidAt, o.TeacherAmount })
                .ToListAsync(cancellationToken);

            var monthlyRevenueList = new List<MonthlyRevenuePointDTO>();
            for (int i = 5; i >= 0; i--)
            {
                var monthDate = DateTime.UtcNow.AddMonths(-i);
                var monthLabel = monthDate.ToString("yyyy-MM");

                var monthRevenue = paidOrders
                    .Where(o => o.PaidAt!.Value.Year == monthDate.Year && o.PaidAt.Value.Month == monthDate.Month)
                    .Sum(o => o.TeacherAmount);

                monthlyRevenueList.Add(new MonthlyRevenuePointDTO
                {
                    Month = monthLabel,
                    Revenue = monthRevenue
                });
            }

            return new TeacherSummaryDTO
            {
                TotalCourses = totalCourses,
                ActiveStudents = activeStudents,
                AverageRating = averageRating,
                TotalRevenue = totalRevenue,
                CourseStatusDistribution = distribution,
                MonthlyRevenue = monthlyRevenueList
            };
        }
    }
}
