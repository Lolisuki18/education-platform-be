using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using Application.Interface;
using Microsoft.EntityFrameworkCore;
using Application.BusinessException;
using System.Linq;

namespace Application.Features.Statistics.Queries.GetSummaryStatistic
{
    public class GetSummaryStatisticQuery : IRequest<SummaryStatisticDTO>
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class GetSummaryStatisticQueryHandler : IRequestHandler<GetSummaryStatisticQuery, SummaryStatisticDTO>
    {
        private readonly IApplicationDBContext _context;

        public GetSummaryStatisticQueryHandler(IApplicationDBContext context)
        {
            _context = context;
        }

        public async Task<SummaryStatisticDTO> Handle(GetSummaryStatisticQuery request, CancellationToken cancellationToken)
        {
            var from = request.From ?? DateTime.MinValue;
            var to = request.To ?? DateTime.MaxValue;

            // 1. User Summary: Remove the "Where" condition to count ALL Users/Teachers/Students on the system.
            var users = await _context.Users
                .AsNoTracking()
                .GroupBy(u => u.Role)
                .Select(g => new { Role = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            // 2. Course Summary: Generally, the total number of courses also needs to be taken all
            var courses = await _context.Courses
                .AsNoTracking()
                .GroupBy(c => c.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            // 3. Enrollment Summary: Get all enrollments from the beginning to the present
            var enrollments = await _context.Enrollments
                .AsNoTracking()
                .GroupBy(e => e.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            // 4. Revenue Summary: Revenue MUST keep the time filter to report by week/month/year
            var revenue = await _context.Orders
                .AsNoTracking()
                .Where(o => o.CreatedAt >= from && o.CreatedAt <= to && o.PaidAt != null)
                .Select(o => new { o.PlatformAmount, o.TeacherAmount })
                .ToListAsync(cancellationToken);

            // 5. Breakdowns: Remove the time filter to display the correct overall structure chart
            var courseByGrade = await _context.Courses
                .AsNoTracking()
                .GroupBy(c => c.Grade.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Name, x => x.Count, cancellationToken);

            var courseBySubject = await _context.Courses
                .AsNoTracking()
                .GroupBy(c => c.Subject.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Name, x => x.Count, cancellationToken);

            var enrollmentByGrade = await _context.Enrollments
                .AsNoTracking()
                .GroupBy(e => e.Course.Grade.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Name, x => x.Count, cancellationToken);

            var enrollmentBySubject = await _context.Enrollments
                .AsNoTracking()
                .GroupBy(e => e.Course.Subject.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Name, x => x.Count, cancellationToken);

            return new SummaryStatisticDTO
            {
                User = new SummaryUserDTO
                {
                    Total = users.Sum(x => x.Count),
                    TeacherCount = users.FirstOrDefault(x => x.Role == Domain.IdentityManagement.Enum.Role.Teacher)?.Count ?? 0,
                    StudentCount = users.FirstOrDefault(x => x.Role == Domain.IdentityManagement.Enum.Role.Student)?.Count ?? 0
                },
                Course = new SummaryCourseDTO
                {
                    Total = courses.Sum(x => x.Count),
                    InReviewCount = courses.FirstOrDefault(x => x.Status == Domain.CourseManagement.Enum.CourseStatus.InReview)?.Count ?? 0,
                    RejectedCount = courses.FirstOrDefault(x => x.Status == Domain.CourseManagement.Enum.CourseStatus.Rejected)?.Count ?? 0,
                    PublishedCount = courses.FirstOrDefault(x => x.Status == Domain.CourseManagement.Enum.CourseStatus.Published)?.Count ?? 0
                },
                Enrollment = new SummaryEnrollmentDTO
                {
                    Total = enrollments.Sum(x => x.Count),
                    Completed = enrollments.FirstOrDefault(x => x.Status == Domain.EnrollmentManagement.Enum.EnrollmentStatus.Completed)?.Count ?? 0,
                    NotCompleted = enrollments.Where(x => x.Status != Domain.EnrollmentManagement.Enum.EnrollmentStatus.Completed).Sum(x => x.Count)
                },
                Revenue = new SummaryRevenueDTO
                {
                    Total = (int)revenue.Sum(x => x.PlatformAmount + x.TeacherAmount),
                    Commission = (int)revenue.Sum(x => x.PlatformAmount),
                    TeacherFinance = (int)revenue.Sum(x => x.TeacherAmount)
                },
                CourseByGrade = new SummaryCourseGradeDTO { GradeCounts = courseByGrade, Total = courseByGrade.Values.Sum() },
                CourseBySubject = new SummaryCourseSubjectDTO { SubjectCounts = courseBySubject, Total = courseBySubject.Values.Sum() },
                EnrollmentByGrade = new SummaryEnrollmentGradeDTO { GradeCounts = enrollmentByGrade, Total = enrollmentByGrade.Values.Sum() },
                EnrollmentBySubject = new SummaryEnrollmentSubjectDTO { SubjectCounts = enrollmentBySubject, Total = enrollmentBySubject.Values.Sum() }
            };
        }
    }
}