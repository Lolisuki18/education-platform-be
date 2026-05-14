using Application.Queries.Statistics;

namespace API.Models.Statistics
{
    public class AnalyticsGrowthRequestDto
    {
        public QueryAnalyticsGrowthDto Query { get; set; } = new();
    }
}
