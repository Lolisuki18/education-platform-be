using Asp.Versioning;
using API.Models.Statistics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Features.Statistics.Queries.GetSummaryStatistics;
using Application.Features.Statistics.Queries.GetAnalyticsGrowth;
using Application.Features.Statistics.Queries.GetAnalyticsDemandAndSupply;
using Application.Features.Statistics.Queries.GetAnalyticsNormalizedGrowth;
using Application.Features.Statistics.Queries.GetTopPerformance;
using Application.Features.Statistics.Queries.GetTeacherSummary;
using AutoMapper;
using API.Models.Common;

namespace API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/statistics")]
    [Route("api/v{version:apiVersion}/statistics")]
    [Authorize]
    public class StatisticsController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly IMapper mapper;

        public StatisticsController(IMediator mediator, IMapper mapper)
        {
            this.mediator = mediator;
            this.mapper = mapper;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("summary")]
        public async Task<ActionResult<ApiResponse<SummaryStatisticsResult>>> GetSummary([FromQuery] GetSummaryStatisticsQuery query)
        {
            var result = await mediator.Send(query, HttpContext.RequestAborted);
            return Ok(ApiResponse<SummaryStatisticsResult>.Success(result));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("analytics/growth")]
        public async Task<ActionResult<ApiResponse<Application.Results.AnalyticsGrowthDTO>>> GetAnalyticsGrowth([FromQuery] GetAnalyticsGrowthQuery query)
        {
            var data = await mediator.Send(query, HttpContext.RequestAborted);
            return Ok(ApiResponse<Application.Results.AnalyticsGrowthDTO>.Success(data));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("analytics/demand-supply")]
        public async Task<ActionResult<ApiResponse<Application.Results.AnalyticsGrowthDTO>>> GetAnalyticsDemandAndSupply([FromQuery] GetAnalyticsDemandAndSupplyQuery query)
        {
            var data = await mediator.Send(query, HttpContext.RequestAborted);
            return Ok(ApiResponse<Application.Results.AnalyticsGrowthDTO>.Success(data));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("analytics/normalized-growth")]
        public async Task<ActionResult<ApiResponse<Application.Results.AnalyticsGrowthDTO>>> GetAnalyticsNormalizedGrowth([FromQuery] GetAnalyticsNormalizedGrowthQuery query)
        {
            var data = await mediator.Send(query, HttpContext.RequestAborted);
            return Ok(ApiResponse<Application.Results.AnalyticsGrowthDTO>.Success(data));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("analytics/top-performance")]
        public async Task<ActionResult<ApiResponse<Application.Results.TopPerformanceDTO>>> GetTopPerformance([FromQuery] GetTopPerformanceQuery query)
        {
            var data = await mediator.Send(query, HttpContext.RequestAborted);
            return Ok(ApiResponse<Application.Results.TopPerformanceDTO>.Success(data));
        }

        [Authorize(Roles = "Teacher")]
        [HttpGet("teacher/summary")]
        public async Task<ActionResult<ApiResponse<TeacherSummaryDTO>>> GetTeacherSummary()
        {
            var result = await mediator.Send(new GetTeacherSummaryQuery(), HttpContext.RequestAborted);
            return Ok(ApiResponse<TeacherSummaryDTO>.Success(result));
        }
    }
}
