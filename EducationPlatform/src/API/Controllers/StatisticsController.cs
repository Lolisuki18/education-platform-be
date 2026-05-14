using API.Models.Statistics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Features.Statistics.Queries.GetSummaryStatistics;
using Application.Features.Statistics.Queries.GetAnalyticsGrowth;
using Application.Features.Statistics.Queries.GetAnalyticsDemandAndSupply;
using Application.Features.Statistics.Queries.GetAnalyticsNormalizedGrowth;
using Application.Features.Statistics.Queries.GetTopPerformance;
using AutoMapper;
using API.Models.Common;

namespace API.Controllers
{
    [ApiController]
    [Route("api/statistics")]
    [Authorize(Roles = "Admin")]
    public class StatisticsController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly IMapper mapper;

        public StatisticsController(IMediator mediator, IMapper mapper)
        {
            this.mediator = mediator;
            this.mapper = mapper;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<ApiResponse<SummaryStatisticsResult>>> GetSummary([FromQuery] GetSummaryStatisticsQuery query)
        {
            var result = await mediator.Send(query);
            return Ok(ApiResponse<SummaryStatisticsResult>.Success(result));
        }

        [HttpGet("analytics/growth")]
        public async Task<ActionResult<ApiResponse<Application.Results.AnalyticsGrowthDTO>>> GetAnalyticsGrowth([FromQuery] GetAnalyticsGrowthQuery query)
        {
            var data = await mediator.Send(query);
            return Ok(ApiResponse<Application.Results.AnalyticsGrowthDTO>.Success(data));
        }

        [HttpGet("analytics/demand-supply")]
        public async Task<ActionResult<ApiResponse<Application.Results.AnalyticsGrowthDTO>>> GetAnalyticsDemandAndSupply([FromQuery] GetAnalyticsDemandAndSupplyQuery query)
        {
            var data = await mediator.Send(query);
            return Ok(ApiResponse<Application.Results.AnalyticsGrowthDTO>.Success(data));
        }

        [HttpGet("analytics/normalized-growth")]
        public async Task<ActionResult<ApiResponse<Application.Results.AnalyticsGrowthDTO>>> GetAnalyticsNormalizedGrowth([FromQuery] GetAnalyticsNormalizedGrowthQuery query)
        {
            var data = await mediator.Send(query);
            return Ok(ApiResponse<Application.Results.AnalyticsGrowthDTO>.Success(data));
        }

        [HttpGet("analytics/top-performance")]
        public async Task<ActionResult<ApiResponse<Application.Results.TopPerformanceDTO>>> GetTopPerformance([FromQuery] GetTopPerformanceQuery query)
        {
            var data = await mediator.Send(query);
            return Ok(ApiResponse<Application.Results.TopPerformanceDTO>.Success(data));
        }
    }
}
