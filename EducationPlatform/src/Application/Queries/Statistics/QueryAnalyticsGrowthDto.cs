using Application.Enums;

namespace Application.Queries.Statistics
{
    public class QueryAnalyticsGrowthDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public AnalyticGroupDate GroupBy { get; set; } = AnalyticGroupDate.Month;
        public List<AnalyticsTimeRangeDto>? ComparisonRanges { get; set; }
        public AnalyticsGrowthType Type { get; set; }

        // ===== Users =====
        public string? UserRole { get; set; }

        // ===== Courses =====
        public Guid? CourseGradeId { get; set; }
        public Guid? CourseSubjectId { get; set; }

        // ===== Enrollments =====
        public Guid? EnrollmentGradeId { get; set; }
        public Guid? EnrollmentSubjectId { get; set; }

        // ===== Revenue =====
        public AnalyticRevenueType? RevenueType { get; set; } = AnalyticRevenueType.All;
    }

    public class AnalyticsTimeRangeDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Label { get; set; } = default!;
    }
}
