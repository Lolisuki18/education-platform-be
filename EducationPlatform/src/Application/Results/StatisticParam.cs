using Application.Enums;

namespace Application.Results
{
    public class QuerySummaryDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class QueryAnalyticsGrowthDto
    {
        public AnalyticsGrowthType Type { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public AnalyticGroupDate GroupBy { get; set; }
        public string? UserRole { get; set; }
        public Guid? CourseGradeId { get; set; }
        public Guid? CourseSubjectId { get; set; }
        public Guid? EnrollmentGradeId { get; set; }
        public Guid? EnrollmentSubjectId { get; set; }
        public AnalyticRevenueType? RevenueType { get; set; }
        public List<ComparisonRangeDTO>? ComparisonRanges { get; set; }
    }

    public class QueryTopPerformanceDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public Guid? GradeId { get; set; }
        public Guid? SubjectId { get; set; }
        public int Top { get; set; } = 10;
    }
}
