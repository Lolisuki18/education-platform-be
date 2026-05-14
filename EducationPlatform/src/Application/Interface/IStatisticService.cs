using Application.Results;
using Application.Queries.Statistics;

namespace Application.Interface
{
    public interface IStatisticService
    {
        public Task<SummaryStatisticDTO> SummaryStatistic(
            QuerySummaryDto dto);

        public Task<AnalyticsGrowthDTO> AnalyticsGrowth(
            QueryAnalyticsGrowthDto dto);

        Task<AnalyticsGrowthDTO> AnalyticsDemandAndSupply(
            QueryAnalyticsGrowthDto dto);

        Task<AnalyticsGrowthDTO> AnalyticsNormalizedGrowth(
            QueryAnalyticsGrowthDto dto);

        Task<TopPerformanceDTO> GetTopPerformance(
            QueryTopPerformanceDto query);
    }
}


