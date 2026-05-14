using Application.Interface;
using Application.Queries.Statistics;
using API.Models.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/statistics")]
    [Authorize(Roles = "Admin")]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticService statisticService;
        private readonly IAcademicService academicService;

        public StatisticsController(
            IStatisticService statisticService,
            IAcademicService academicService)
        {
            this.statisticService = statisticService;
            this.academicService = academicService;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<SummaryStatisticsResponseDto>> GetSummary([FromQuery] SummaryStatisticsRequestDto request)
        {
            request.Query.From ??= DateTime.Now.AddMonths(-1);
            request.Query.To ??= DateTime.Now;

            var grades = await academicService.GetGrades();
            var subjects = await academicService.GetSubjects();
            var summary = await statisticService.SummaryStatistic(request.Query);

            return Ok(new SummaryStatisticsResponseDto
            {
                Grades = grades,
                Subjects = subjects,
                Summary = summary
            });
        }

        [HttpGet("analytics/growth")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetAnalyticsGrowth([FromQuery] AnalyticsGrowthRequestDto request)
        {
            var data = await statisticService.AnalyticsGrowth(request.Query);
            return Ok(new AnalyticsResponseDto { Data = data });
        }

        [HttpGet("analytics/demand-supply")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetAnalyticsDemandAndSupply([FromQuery] AnalyticsGrowthRequestDto request)
        {
            var data = await statisticService.AnalyticsDemandAndSupply(request.Query);
            return Ok(new AnalyticsResponseDto { Data = data });
        }

        [HttpGet("analytics/normalized-growth")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetAnalyticsNormalizedGrowth([FromQuery] AnalyticsGrowthRequestDto request)
        {
            var data = await statisticService.AnalyticsNormalizedGrowth(request.Query);
            return Ok(new AnalyticsResponseDto { Data = data });
        }

        [HttpGet("analytics/top-performance")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetTopPerformance([FromQuery] TopPerformanceRequestDto request)
        {
            var data = await statisticService.GetTopPerformance(request.Query);
            return Ok(new AnalyticsResponseDto { Data = data });
        }
    }
}
