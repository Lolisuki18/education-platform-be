using Application.Results;

namespace API.Models.Statistics
{
    public class TopPerformanceRequestDto
    {
        public QueryTopPerformanceDto Query { get; set; } = new();
    }
}
