using Application.Results;

namespace API.Models.Statistics
{
    public class SummaryStatisticsRequestDto
    {
        public QuerySummaryDto Query { get; set; } = new();
    }
}
