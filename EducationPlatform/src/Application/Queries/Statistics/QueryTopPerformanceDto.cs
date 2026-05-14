using Application.Enums;

namespace Application.Queries.Statistics
{
    public class QueryTopPerformanceDto
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public TopPerformanceGroupBy GroupBy { get; set; } = TopPerformanceGroupBy.OfAllTime;
        public Guid? GradeId { get; set; }
        public Guid? SubjectId { get; set; }
        public int Top { get; set; } = 10;
    }
}
