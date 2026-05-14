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
        public async Task<ActionResult<SummaryStatisticsResponseDto>> GetSummary([FromQuery] SummaryStatisticsRequestDto request)
        {
            var query = mapper.Map<GetSummaryStatisticsQuery>(request);
            var result = await mediator.Send(query);

            return Ok(new SummaryStatisticsResponseDto
            {
                Grades = result.Grades,
                Subjects = result.Subjects,
                Summary = result.Summary
            });
        }

        [HttpGet("analytics/growth")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetAnalyticsGrowth([FromQuery] AnalyticsGrowthRequestDto request)
        {
            var query = mapper.Map<GetAnalyticsGrowthQuery>(request);
            var data = await mediator.Send(query);
            return Ok(new AnalyticsResponseDto { Data = data });
        }

        [HttpGet("analytics/demand-supply")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetAnalyticsDemandAndSupply([FromQuery] AnalyticsGrowthRequestDto request)
        {
            var query = mapper.Map<GetAnalyticsDemandAndSupplyQuery>(request);
            var data = await mediator.Send(query);
            return Ok(new AnalyticsResponseDto { Data = data });
        }

        [HttpGet("analytics/normalized-growth")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetAnalyticsNormalizedGrowth([FromQuery] AnalyticsGrowthRequestDto request)
        {
            var query = mapper.Map<GetAnalyticsNormalizedGrowthQuery>(request);
            var data = await mediator.Send(query);
            return Ok(new AnalyticsResponseDto { Data = data });
        }

        [HttpGet("analytics/top-performance")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetTopPerformance([FromQuery] TopPerformanceRequestDto request)
        {
            var query = mapper.Map<GetTopPerformanceQuery>(request);
            var data = await mediator.Send(query);
            return Ok(new AnalyticsResponseDto { Data = data });
        }
    }
}
