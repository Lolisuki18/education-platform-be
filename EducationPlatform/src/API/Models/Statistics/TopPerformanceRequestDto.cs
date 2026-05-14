using Application.Queries.Statistics;

namespace API.Models.Statistics
{
    public class TopPerformanceRequestDto
    {
        public QueryTopPerformanceDto Query { get; set; } = new();
    }
}
